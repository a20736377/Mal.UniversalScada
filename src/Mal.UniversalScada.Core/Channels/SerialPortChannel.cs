using System.IO.Ports;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Channels;

/// <summary>
/// 基于物理/虚拟串口通信通道实现 (IChannel)
/// 全面支持 RS485/RS232/USB-Serial，具备 DTR/RTS 控制、高可靠异步收发与超时防护
/// </summary>
public class SerialPortChannel : IChannel
{
    private readonly ChannelConfig _config;
    private SerialPort? _serial;
    private ChannelState _state = ChannelState.Disconnected;
    private readonly object _stateLock = new();

    public string ChannelId => _config.ChannelId;
    public ChannelState State => _state;
    public bool IsOpen => _serial != null && _serial.IsOpen;

    public event EventHandler<ChannelState>? StateChanged;

    public SerialPortChannel(ChannelConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public Task<bool> OpenAsync(CancellationToken ct = default)
    {
        if (IsOpen) return Task.FromResult(true);

        SetState(ChannelState.Connecting);
        try
        {
            Enum.TryParse<StopBits>(_config.StopBits, true, out var stopBits);
            Enum.TryParse<Parity>(_config.Parity, true, out var parity);

            _serial = new SerialPort(
                _config.PortName,
                _config.BaudRate,
                parity == default ? Parity.None : parity,
                _config.DataBits > 0 ? _config.DataBits : 8,
                stopBits == default ? StopBits.One : stopBits)
            {
                ReadTimeout = _config.ReadTimeoutMs > 0 ? _config.ReadTimeoutMs : 2000,
                WriteTimeout = _config.WriteTimeoutMs > 0 ? _config.WriteTimeoutMs : 2000,
                // 关键：启用 DTR/RTS 以保证大部分 USB-RS485 转换芯片及半双工收发自如供电与触发
                DtrEnable = true,
                RtsEnable = true,
                Handshake = Handshake.None
            };

            _serial.Open();
            ClearBuffer();
            SetState(ChannelState.Connected);
            return Task.FromResult(true);
        }
        catch (Exception)
        {
            SetState(ChannelState.Faulted);
            Cleanup();
            return Task.FromResult(false);
        }
    }

    public Task CloseAsync()
    {
        Cleanup();
        SetState(ChannelState.Disconnected);
        return Task.CompletedTask;
    }

    public async Task<int> SendAsync(byte[] buffer, int offset, int count, CancellationToken ct = default)
    {
        if (!IsOpen || _serial == null) throw new InvalidOperationException($"串口通道 [{ChannelId}] 未处于打开状态");

        using var timeoutCts = new CancellationTokenSource(_serial.WriteTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await _serial.BaseStream.WriteAsync(buffer.AsMemory(offset, count), linkedCts.Token);
            await _serial.BaseStream.FlushAsync(linkedCts.Token);
            return count;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"串口通道 [{ChannelId}] 发送数据超时 ({_serial.WriteTimeout} ms)");
        }
    }

    public async Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken ct = default)
    {
        if (!IsOpen || _serial == null) throw new InvalidOperationException($"串口通道 [{ChannelId}] 未处于打开状态");

        // 若缓冲区已有就绪数据，优先同步快速读取
        if (_serial.BytesToRead > 0)
        {
            int toRead = Math.Min(count, _serial.BytesToRead);
            return _serial.Read(buffer, offset, toRead);
        }

        // 无就绪数据时，挂起异步等待，并绑定串口配置的 ReadTimeout 超时机制
        using var timeoutCts = new CancellationTokenSource(_serial.ReadTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            return await _serial.BaseStream.ReadAsync(buffer.AsMemory(offset, count), linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"串口通道 [{ChannelId}] 读取数据超时 ({_serial.ReadTimeout} ms)");
        }
    }

    public void ClearBuffer()
    {
        try
        {
            if (_serial is { IsOpen: true })
            {
                _serial.DiscardInBuffer();
                _serial.DiscardOutBuffer();
            }
        }
        catch
        {
            // 清理残留缓冲异常静默忽略
        }
    }

    private void SetState(ChannelState newState)
    {
        lock (_stateLock)
        {
            if (_state != newState)
            {
                _state = newState;
                StateChanged?.Invoke(this, newState);
            }
        }
    }

    private void Cleanup()
    {
        try
        {
            if (_serial is { IsOpen: true })
            {
                _serial.Close();
            }
            _serial?.Dispose();
        }
        catch { }
        _serial = null;
    }

    public void Dispose()
    {
        Cleanup();
        SetState(ChannelState.Disconnected);
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
