using System.Buffers.Binary;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Drivers.Modbus.Protocol.Framers;

/// <summary>
/// Modbus TCP 报文帧封装器 (MBAP 报头: TransactionID + ProtocolID + Length + UnitID)
/// </summary>
public class MbapFramer : IModbusFramer
{
    private ushort _transactionId;

    public ProtocolType ProtocolType => ProtocolType.ModbusTcp;

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

        ushort txId = unchecked(++_transactionId);
        if (txId == 0) txId = ++_transactionId;

        // 构建 MBAP 请求帧: 7 字节头 + PDU
        // Length = 1 (UnitID) + PDU.Length
        ushort mbapLength = (ushort)(1 + pdu.Length);
        var sendFrame = new byte[6 + mbapLength];

        BinaryPrimitives.WriteUInt16BigEndian(sendFrame.AsSpan(0, 2), txId);
        BinaryPrimitives.WriteUInt16BigEndian(sendFrame.AsSpan(2, 2), 0); // Protocol ID = 0 (Modbus)
        BinaryPrimitives.WriteUInt16BigEndian(sendFrame.AsSpan(4, 2), mbapLength);
        sendFrame[6] = slaveId;
        pdu.CopyTo(sendFrame.AsSpan(7));

        await channel.SendAsync(sendFrame, 0, sendFrame.Length, token);

        // 接收 MBAP 报头 (前 6 字节: TxId(2), ProtoId(2), Length(2))
        var mbapHeader = new byte[6];
        await ReadExactBytesAsync(channel, mbapHeader, 0, 6, token);

        var respTxId = BinaryPrimitives.ReadUInt16BigEndian(mbapHeader.AsSpan(0, 2));
        var respProtoId = BinaryPrimitives.ReadUInt16BigEndian(mbapHeader.AsSpan(2, 2));
        var respLength = BinaryPrimitives.ReadUInt16BigEndian(mbapHeader.AsSpan(4, 2));

        if (respLength < 2 || respLength > 260)
        {
            throw new InvalidOperationException($"收到的 Modbus TCP 报文标称长度非法: {respLength}");
        }

        // 接收剩余的 respLength 字节 (包含 1 字节 UnitId 和响应 PDU)
        var remainingBytes = new byte[respLength];
        await ReadExactBytesAsync(channel, remainingBytes, 0, respLength, token);

        var respUnitId = remainingBytes[0];
        // 提取响应 PDU
        var respPdu = remainingBytes.AsSpan(1).ToArray();

        return respPdu;
    }

    private static async Task ReadExactBytesAsync(
        IChannel channel, 
        byte[] buffer, 
        int offset, 
        int count, 
        CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await channel.ReceiveAsync(buffer, offset + totalRead, count - totalRead, ct);
            if (read <= 0)
            {
                throw new IOException($"Modbus TCP 连接中断，期望读取 {count} 字节，实际仅读取 {totalRead} 字节");
            }
            totalRead += read;
        }
    }
}
