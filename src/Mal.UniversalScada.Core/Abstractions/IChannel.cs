using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 物理/网络通信通道（链路层）统一抽象接口。
/// 封装具体传输介质（如串口 COM、TCP 客户端/服务端、UDP、工业总线卡等），负责底层二进制流收发。
/// </summary>
public interface IChannel : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 获取通道唯一标识符 (例如: "COM1_9600" 或 "TCP_192.168.1.100_502")
    /// </summary>
    string ChannelId { get; }

    /// <summary>
    /// 获取当前通道的连接状态
    /// </summary>
    ChannelState State { get; }

    /// <summary>
    /// 获取当前通道是否处于打开/连接就绪状态
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// 异步打开通道建立物理连接
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>若成功打开或已处于打开状态返回 true，失败返回 false</returns>
    Task<bool> OpenAsync(CancellationToken ct = default);

    /// <summary>
    /// 异步关闭通道断开连接
    /// </summary>
    /// <returns>异步任务</returns>
    Task CloseAsync();

    /// <summary>
    /// 异步向通道发送原始二进制字节流
    /// </summary>
    /// <param name="buffer">要发送的数据缓冲区</param>
    /// <param name="offset">起始偏移位置</param>
    /// <param name="count">发送的字节数量</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>实际成功发送的字节数</returns>
    Task<int> SendAsync(byte[] buffer, int offset, int count, CancellationToken ct = default);

    /// <summary>
    /// 异步从通道接收原始二进制字节流
    /// </summary>
    /// <param name="buffer">用于存放接收数据的缓冲区</param>
    /// <param name="offset">存放起始偏移位置</param>
    /// <param name="count">最大期望读取的字节数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>实际接收到的字节数</returns>
    Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken ct = default);

    /// <summary>
    /// 清空通道中的接收和发送缓冲区残留数据
    /// </summary>
    void ClearBuffer();

    /// <summary>
    /// 通道连接状态变更时触发的事件通知
    /// </summary>
    event EventHandler<ChannelState>? StateChanged;
}
