using System.IO.Ports;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Channels;

/// <summary>
/// 基于物理/虚拟串口通信通道实现 (IChannel)
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
                stopBits == default ? StopBits.One : stopBits);

            _serial.ReadTimeout = _config.ReadTimeoutMs > 0 ? _config.ReadTimeoutMs : 2000;
            _serial.WriteTimeout = _config.WriteTimeoutMs > 0 ? _config.WriteTimeoutMs : 2000;

            _serial.Open();
            SetState(ChannelState.Connected);
            return Task.FromResult(true);
        }
        catch
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
        await _serial.BaseStream.WriteAsync(buffer.AsMemory(offset, count), ct);
        await _serial.BaseStream.FlushAsync(ct);
        return count;
    }

    public async Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken ct = default)
    {
        if (!IsOpen || _serial == null) throw new InvalidOperationException($"串口通道 [{ChannelId}] 未处于打开状态");
        return await _serial.BaseStream.ReadAsync(buffer.AsMemory(offset, count), ct);
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
