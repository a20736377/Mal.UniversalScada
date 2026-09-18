using System.Buffers.Binary;
using Mal.UniversalScada.Core.Abstractions;

namespace Mal.UniversalScada.Drivers.Siemens.Protocol;

/// <summary>
/// ISO-on-TCP (RFC 1006 / TPKT) 与 COTP (ISO 8073) 连接管理与报文封装器
/// </summary>
public static class CotpHandler
{
    private const byte TpktVersion = 0x03;
    private const byte CotpCr = 0xE0; // Connection Request
    private const byte CotpCc = 0xD0; // Connection Confirm
    private const byte CotpDt = 0xF0; // Data TPDU

    /// <summary>
    /// 构建 COTP Connection Request (CR) 握手连接请求报文
    /// </summary>
    public static byte[] BuildConnectionRequest(byte[] srcTsap, byte[] dstTsap)
    {
        // COTP Header 部分参数:
        // PDU Type (1) + DstRef (2) + SrcRef (2) + Class (1) = 6
        // TPDU Size: Param (1) + Len (1) + Val (1) = 3
        // Src TSAP: Param (1) + Len (1) + srcTsap.Length
        // Dst TSAP: Param (1) + Len (1) + dstTsap.Length
        var cotpLengthIndicator = (byte)(6 + 3 + 2 + srcTsap.Length + 2 + dstTsap.Length); // LI 为 LI 后续所有 COTP 报头字节数

        var totalLen = 4 + 1 + cotpLengthIndicator; // 4 bytes TPKT + LI (1) + COTP
        var packet = new byte[totalLen];

        // 1. TPKT Header (4 bytes)
        packet[0] = TpktVersion;
        packet[1] = 0x00; // Reserved
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), (ushort)totalLen);

        // 2. COTP Header
        packet[4] = cotpLengthIndicator;
        packet[5] = CotpCr; // 0xE0 Connection Request
        packet[6] = 0x00;   // Dst Ref Hi
        packet[7] = 0x00;   // Dst Ref Lo
        packet[8] = 0x00;   // Src Ref Hi
        packet[9] = 0x01;   // Src Ref Lo
        packet[10] = 0x00;  // Class 0

        // 3. Parameter: TPDU Size (0xC0, Len: 1, 0x0A => 1024 bytes)
        packet[11] = 0xC0;
        packet[12] = 0x01;
        packet[13] = 0x0A;

        // 4. Parameter: Calling TSAP (Src TSAP)
        int idx = 14;
        packet[idx++] = 0xC1;
        packet[idx++] = (byte)srcTsap.Length;
        Buffer.BlockCopy(srcTsap, 0, packet, idx, srcTsap.Length);
        idx += srcTsap.Length;

        // 5. Parameter: Called TSAP (Dst TSAP)
        packet[idx++] = 0xC2;
        packet[idx++] = (byte)dstTsap.Length;
        Buffer.BlockCopy(dstTsap, 0, packet, idx, dstTsap.Length);

        return packet;
    }

    /// <summary>
    /// 校验从 PLC 收到的 COTP Connection Confirm (CC) 响应
    /// </summary>
    public static bool ValidateConnectionConfirm(byte[] buffer, int length)
    {
        if (length < 7) return false;

        // 校验 TPKT 版本
        if (buffer[0] != TpktVersion || buffer[1] != 0x00) return false;

        // 校验 COTP PDU 类型 (偏移量 5 处应为 0xD0 Connection Confirm)
        if (buffer[5] != CotpCc) return false;

        return true;
    }

    /// <summary>
    /// 将上层 S7 PDU 包装为符合 RFC 1006 标准的 ISO-on-TCP (TPKT + COTP DT) 报文
    /// </summary>
    public static byte[] WrapDataPdu(byte[] s7Pdu)
    {
        var totalLen = 4 + 3 + s7Pdu.Length; // 4 TPKT + 3 COTP DT + S7 PDU
        var packet = new byte[totalLen];

        // 1. TPKT Header
        packet[0] = TpktVersion;
        packet[1] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), (ushort)totalLen);

        // 2. COTP DT Header
        packet[4] = 0x02;   // COTP Header Length
        packet[5] = CotpDt; // 0xF0 Data TPDU
        packet[6] = 0x80;   // TPDU-NR / EOT (0x80 = 最后一帧)

        // 3. S7 PDU
        Buffer.BlockCopy(s7Pdu, 0, packet, 7, s7Pdu.Length);
        return packet;
    }

    /// <summary>
    /// 从 ISO-on-TCP 接收帧中剥离 TPKT 与 COTP 头，提取纯粹的 S7 PDU
    /// </summary>
    public static ReadOnlyMemory<byte> UnwrapDataPdu(byte[] buffer, int length)
    {
        if (length < 7)
        {
            throw new InvalidOperationException($"收到的报文长度 ({length}) 小于 TPKT/COTP 最小报头长度 (7)");
        }

        if (buffer[0] != TpktVersion)
        {
            throw new InvalidOperationException($"无效的 TPKT 版本标识: 0x{buffer[0]:X2}");
        }

        var cotpLen = buffer[4];
        var cotpType = buffer[5];

        if (cotpType != CotpDt)
        {
            throw new InvalidOperationException($"期望收到 COTP DT (0xF0) 数据帧，实际收到: 0x{cotpType:X2}");
        }

        var s7PduOffset = 4 + 1 + cotpLen;
        var s7PduLength = length - s7PduOffset;

        if (s7PduLength < 0)
        {
            throw new InvalidOperationException("报文长度异常，S7 PDU 数据长度为负");
        }

        return new ReadOnlyMemory<byte>(buffer, s7PduOffset, s7PduLength);
    }

    /// <summary>
    /// 严格按照 RFC 1006 TPKT 长度指示从 IChannel 读取一个完整的 ISO-on-TCP 数据包
    /// 杜绝 TCP 粘包与拆包导致的协议帧残缺
    /// </summary>
    public static async Task<byte[]> ReceiveFullFrameAsync(IChannel channel, CancellationToken ct)
    {
        // 1. 先读取 4 字节的 TPKT 头部
        var header = new byte[4];
        var readTotal = 0;
        while (readTotal < 4)
        {
            var read = await channel.ReceiveAsync(header, readTotal, 4 - readTotal, ct);
            if (read <= 0)
            {
                throw new EndOfStreamException("通道在读取 TPKT 报头期间已被对端关闭");
            }
            readTotal += read;
        }

        if (header[0] != TpktVersion)
        {
            throw new InvalidOperationException($"非法 TPKT 帧头: 0x{header[0]:X2}");
        }

        // 2. 解析总长度
        var totalLength = (int)BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        if (totalLength < 4 || totalLength > 4096)
        {
            throw new InvalidOperationException($"非法 TPKT 报文声明长度: {totalLength}");
        }

        // 3. 读取剩余数据 payload (totalLength - 4)
        var fullPacket = new byte[totalLength];
        Buffer.BlockCopy(header, 0, fullPacket, 0, 4);

        readTotal = 4;
        while (readTotal < totalLength)
        {
            var read = await channel.ReceiveAsync(fullPacket, readTotal, totalLength - readTotal, ct);
            if (read <= 0)
            {
                throw new EndOfStreamException("通道在读取完整 ISO-on-TCP 报文体期间已被对端关闭");
            }
            readTotal += read;
        }

        return fullPacket;
    }
}
