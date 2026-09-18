using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Drivers.Modbus.Protocol.Framers;

/// <summary>
/// Modbus RTU 串口报文帧封装器 (从站号 + PDU + CRC16 校验码)
/// 严格遵循工业级 RTU 字节时序与帧长度解析
/// </summary>
public class RtuFramer : IModbusFramer
{
    public ProtocolType ProtocolType => ProtocolType.ModbusRtu;

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

        // 构建 RTU 发送帧: [SlaveId, Pdu..., CrcLo, CrcHi]
        var frameLen = 1 + pdu.Length + 2;
        var sendFrame = new byte[frameLen];
        sendFrame[0] = slaveId;
        pdu.CopyTo(sendFrame.AsSpan(1));

        ModbusCrc.AppendCrc(sendFrame, sendFrame.AsSpan(0, 1 + pdu.Length));

        // 发送完整 RTU 帧
        await channel.SendAsync(sendFrame, 0, sendFrame.Length, token);

        // 接收响应并按功能码动态组帧
        var header = new byte[2]; // [SlaveId, FC]
        await ReadExactBytesAsync(channel, header, 0, 2, token);

        var respSlave = header[0];
        var respFc = header[1];

        byte[] fullFrame;

        // 1. 异常响应: FC 最高位置 1，固定为 5 字节 [SlaveId, FC, ErrCode, CrcLo, CrcHi]
        if ((respFc & 0x80) != 0)
        {
            fullFrame = new byte[5];
            fullFrame[0] = respSlave;
            fullFrame[1] = respFc;
            await ReadExactBytesAsync(channel, fullFrame, 2, 3, token);
        }
        // 2. 读操作响应 (FC 01, 02, 03, 04): 接下来是 ByteCount(1B) + Data(ByteCount B) + CRC(2B)
        else if (respFc is ModbusPduCodec.FcReadCoils or ModbusPduCodec.FcReadDiscreteInputs 
                          or ModbusPduCodec.FcReadHoldingRegisters or ModbusPduCodec.FcReadInputRegisters)
        {
            var byteCountBuf = new byte[1];
            await ReadExactBytesAsync(channel, byteCountBuf, 0, 1, token);
            var byteCount = byteCountBuf[0];

            var totalLen = 1 + 1 + 1 + byteCount + 2;
            fullFrame = new byte[totalLen];
            fullFrame[0] = respSlave;
            fullFrame[1] = respFc;
            fullFrame[2] = byteCount;

            await ReadExactBytesAsync(channel, fullFrame, 3, byteCount + 2, token);
        }
        // 3. 写操作响应 (FC 05, 06, 15, 16): 固定响应 8 字节 [Slave, FC, 4 字节数据, 2 字节 CRC]
        else if (respFc is ModbusPduCodec.FcWriteSingleCoil or ModbusPduCodec.FcWriteSingleRegister
                          or ModbusPduCodec.FcWriteMultipleCoils or ModbusPduCodec.FcWriteMultipleRegisters)
        {
            fullFrame = new byte[8];
            fullFrame[0] = respSlave;
            fullFrame[1] = respFc;
            await ReadExactBytesAsync(channel, fullFrame, 2, 6, token);
        }
        else
        {
            throw new InvalidOperationException($"收到了未知的 Modbus RTU 功能码: 0x{respFc:X2}");
        }

        // 校验 CRC16
        if (!ModbusCrc.Validate(fullFrame))
        {
            throw new InvalidOperationException($"Modbus RTU 报文 CRC16 校验失败");
        }

        // 提取响应 PDU: 去除首字节 SlaveId 和末尾 2 字节 CRC
        var pduLen = fullFrame.Length - 3;
        var respPdu = new byte[pduLen];
        Array.Copy(fullFrame, 1, respPdu, 0, pduLen);

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
                throw new IOException($"Modbus RTU 通道接收中断，期望读取 {count} 字节，实际仅读取 {totalRead} 字节");
            }
            totalRead += read;
        }
    }
}
