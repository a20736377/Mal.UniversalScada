using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Drivers.Modbus.Protocol.Framers;

/// <summary>
/// Modbus ASCII 报文帧封装器 (冒号起始 + HEX 编码 + LRC 纵向冗余校验 + CRLF 结束)
/// </summary>
public class AsciiFramer : IModbusFramer
{
    public ProtocolType ProtocolType => ProtocolType.ModbusAscii;

    public async Task<byte[]> SendAndReceivePduAsync(
        IChannel channel,
        byte slaveId,
        byte[] pdu,
        int timeoutMs,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(pdu);

        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var token = linkedCts.Token;

        var sendFrame = ModbusLrc.EncodeAsciiFrame(slaveId, pdu);
        await channel.SendAsync(sendFrame, 0, sendFrame.Length, token);

        // 持续接收字符流直到遇到 '\n' (0x0A)
        var buffer = new List<byte>(128);
        var oneByte = new byte[1];

        while (!token.IsCancellationRequested)
        {
            var read = await channel.ReceiveAsync(oneByte, 0, 1, token);
            if (read <= 0)
            {
                throw new IOException("Modbus ASCII 通道接收中断");
            }

            buffer.Add(oneByte[0]);
            if (oneByte[0] == (byte)'\n')
            {
                break;
            }

            if (buffer.Count > 1024)
            {
                throw new InvalidOperationException("Modbus ASCII 报文超出单帧最大长度限制");
            }
        }

        if (!ModbusLrc.TryDecodeAsciiFrame(buffer.ToArray(), out var respSlave, out var respPdu, out var error))
        {
            throw new InvalidOperationException($"Modbus ASCII 响应解析失败: {error}");
        }

        return respPdu;
    }
}
