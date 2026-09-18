using System.Net.Sockets;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Channels;

/// <summary>
/// 基于 TCP 客户端以太网通信通道实现 (IChannel)
/// </summary>
public class TcpClientChannel : IChannel
{
    private readonly ChannelConfig _config;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private ChannelState _state = ChannelState.Disconnected;
    private readonly object _stateLock = new();

    public string ChannelId => _config.ChannelId;
    public ChannelState State => _state;
    public bool IsOpen => _client != null && _client.Connected && _stream != null;

    public event EventHandler<ChannelState>? StateChanged;

    public TcpClientChannel(ChannelConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<bool> OpenAsync(CancellationToken ct = default)
    {
        if (IsOpen) return true;

        SetState(ChannelState.Connecting);
        try
        {
            _client = new TcpClient();
            var timeoutMs = _config.ReadTimeoutMs > 0 ? _config.ReadTimeoutMs : 3000;
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await _client.ConnectAsync(_config.Host, _config.Port, linked.Token);
            _client.ReceiveTimeout = timeoutMs;
            _client.SendTimeout = _config.WriteTimeoutMs > 0 ? _config.WriteTimeoutMs : 3000;
            _stream = _client.GetStream();

            SetState(ChannelState.Connected);
            return true;
        }
        catch
        {
            SetState(ChannelState.Faulted);
            Cleanup();
            return false;
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
        if (!IsOpen || _stream == null) throw new InvalidOperationException($"TCP 通道 [{ChannelId}] 未建立连接或已被关闭");
        await _stream.WriteAsync(buffer.AsMemory(offset, count), ct);
        await _stream.FlushAsync(ct);
        return count;
    }

    public async Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken ct = default)
    {
        if (!IsOpen || _stream == null) throw new InvalidOperationException($"TCP 通道 [{ChannelId}] 未建立连接或已被关闭");
        return await _stream.ReadAsync(buffer.AsMemory(offset, count), ct);
    }

    public void ClearBuffer()
    {
        try
        {
            if (_stream != null && _stream.DataAvailable)
            {
                var tmp = new byte[1024];
                while (_stream.DataAvailable)
                {
                    _stream.Read(tmp, 0, tmp.Length);
                }
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
        try { _stream?.Dispose(); } catch { }
        try { _client?.Close(); _client?.Dispose(); } catch { }
        _stream = null;
        _client = null;
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
