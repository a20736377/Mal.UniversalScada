using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Drivers.Modbus.Protocol.Framers;

/// <summary>
/// Modbus 报文帧封装与收发抽象接口 (支持 TCP MBAP, RTU 串口 CRC, ASCII 串口 LRC)
/// </summary>
public interface IModbusFramer
{
    /// <summary>
    /// 当前帧封装器对应的协议类型
    /// </summary>
    ProtocolType ProtocolType { get; }

    /// <summary>
    /// 通过通信通道发送 Modbus 请求 PDU 并接收并解包返回响应 PDU
    /// </summary>
    /// <param name="channel">物理/网络通道</param>
    /// <param name="slaveId">从站号/单元号</param>
    /// <param name="pdu">请求功能码与数据 (PDU)</param>
    /// <param name="timeoutMs">超时时间 (毫秒)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>从站响应的纯 PDU 字节数据</returns>
    Task<byte[]> SendAndReceivePduAsync(
        IChannel channel,
        byte slaveId,
        byte[] pdu,
        int timeoutMs,
        CancellationToken ct);
}
