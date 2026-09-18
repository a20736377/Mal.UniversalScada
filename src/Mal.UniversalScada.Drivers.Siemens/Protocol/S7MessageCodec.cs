using System.Buffers.Binary;
using Mal.UniversalScada.Drivers.Siemens.Enums;
using Mal.UniversalScada.Drivers.Siemens.Models;

namespace Mal.UniversalScada.Drivers.Siemens.Protocol;

/// <summary>
/// 西门子单项/批量读取应答结果项
/// </summary>
public record S7ReadResultItem
{
    /// <summary>
    /// S7 返回码 (0xFF 为成功)
    /// </summary>
    public S7ReturnCode ReturnCode { get; init; } = S7ReturnCode.Success;

    /// <summary>
    /// 是否执行成功
    /// </summary>
    public bool IsSuccess => ReturnCode == S7ReturnCode.Success;

    /// <summary>
    /// 原始数据字节切片
    /// </summary>
    public byte[] Data { get; init; } = [];

    /// <summary>
    /// 是否为布尔位结果
    /// </summary>
    public bool IsBit { get; init; }
}

/// <summary>
/// 西门子 S7 协议 PDU 编解码器
/// </summary>
public static class S7MessageCodec
{
    private const byte S7ProtocolId = 0x32;
    private const byte RosctrJob = 0x01;
    private const byte RosctrAckData = 0x03;

    private const byte FuncSetupComm = 0xF0;
    private const byte FuncReadVar = 0x04;
    private const byte FuncWriteVar = 0x05;

    /// <summary>
    /// 构建 S7 Setup Communication 协商握手 PDU
    /// </summary>
    public static byte[] BuildSetupCommunicationPdu(ushort pduRef, ushort preferredPduLength = 960)
    {
        var pdu = new byte[10 + 8]; // 10 字节 Header + 8 字节 Parameter

        // S7 Header (10 bytes)
        pdu[0] = S7ProtocolId;
        pdu[1] = RosctrJob;
        pdu[2] = 0x00; // Redundancy Hi
        pdu[3] = 0x00; // Redundancy Lo
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(4, 2), pduRef);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(6, 2), 8);  // Parameter Length = 8
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(8, 2), 0);  // Data Length = 0

        // Parameter (8 bytes)
        pdu[10] = FuncSetupComm; // 0xF0
        pdu[11] = 0x00;          // Reserved
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(12, 2), 1); // Max AmQ calling
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(14, 2), 1); // Max AmQ called
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(16, 2), preferredPduLength); // PDU length

        return pdu;
    }

    /// <summary>
    /// 解析 S7 Setup Communication 应答并提取最终协商的 PDU 长度
    /// </summary>
    public static ushort ParseSetupCommunicationAck(ReadOnlySpan<byte> s7Pdu)
    {
        if (s7Pdu.Length < 12 + 8)
        {
            throw new InvalidOperationException($"Setup Communication 响应过短: {s7Pdu.Length} 字节");
        }

        if (s7Pdu[0] != S7ProtocolId || s7Pdu[1] != RosctrAckData)
        {
            throw new InvalidOperationException($"无效的 S7 Setup 应答帧头: ProtocolId=0x{s7Pdu[0]:X2}, ROSCTR=0x{s7Pdu[1]:X2}");
        }

        var errorClass = s7Pdu[10];
        var errorCode = s7Pdu[11];
        if (errorClass != 0 || errorCode != 0)
        {
            throw new InvalidOperationException($"S7 Setup 协商失败: ErrorClass=0x{errorClass:X2}, ErrorCode=0x{errorCode:X2}");
        }

        // Parameter 起始偏移为 12 字节
        var func = s7Pdu[12];
        if (func != FuncSetupComm)
        {
            throw new InvalidOperationException($"S7 Setup 功能码不匹配: 0x{func:X2}");
        }

        // Parameter 的 bytes 6-7 (即 S7 PDU 的 18, 19) 为协商后的 PDU Length
        var negotiatedPduLen = BinaryPrimitives.ReadUInt16BigEndian(s7Pdu.Slice(18, 2));
        return negotiatedPduLen > 0 ? negotiatedPduLen : (ushort)240;
    }

    /// <summary>
    /// 构建 S7 Read Variable (0x04) 批量读取请求 PDU
    /// </summary>
    public static byte[] BuildReadVarPdu(ushort pduRef, IReadOnlyList<S7Address> addresses)
    {
        if (addresses == null || addresses.Count == 0)
        {
            throw new ArgumentException("待读取地址列表不能为空", nameof(addresses));
        }

        var paramLen = (ushort)(2 + (addresses.Count * 12));
        var totalLen = 10 + paramLen;
        var pdu = new byte[totalLen];

        // S7 Header (10 bytes)
        pdu[0] = S7ProtocolId;
        pdu[1] = RosctrJob;
        pdu[2] = 0x00;
        pdu[3] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(4, 2), pduRef);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(6, 2), paramLen);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(8, 2), 0); // Data Length = 0

        // Parameter Header (2 bytes)
        pdu[10] = FuncReadVar;
        pdu[11] = (byte)addresses.Count;

        // Items (每个 12 字节)
        int idx = 12;
        foreach (var addr in addresses)
        {
            pdu[idx++] = 0x12; // Variable specification
            pdu[idx++] = 0x0A; // Length of following address specification
            pdu[idx++] = 0x10; // Syntax ID: S7ANY

            if (addr.IsBit)
            {
                pdu[idx++] = (byte)S7TransportSize.Bit; // 0x01
                BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(idx, 2), 1); // 1 bit
            }
            else
            {
                pdu[idx++] = (byte)S7TransportSize.Byte; // 0x02
                BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(idx, 2), (ushort)addr.ByteLength);
            }
            idx += 2;

            // DB Number (2 bytes)
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(idx, 2), (ushort)addr.DbNumber);
            idx += 2;

            // Area Code (1 byte)
            pdu[idx++] = (byte)addr.Area;

            // Address (3 bytes, Big-Endian): 24-bit (ByteOffset * 8 + BitOffset)
            int bitOffset = addr.S7ByteBitOffset;
            pdu[idx++] = (byte)((bitOffset >> 16) & 0xFF);
            pdu[idx++] = (byte)((bitOffset >> 8) & 0xFF);
            pdu[idx++] = (byte)(bitOffset & 0xFF);
        }

        return pdu;
    }

    /// <summary>
    /// 解析 S7 Read Variable (0x04) Ack-Data 应答报文
    /// </summary>
    public static List<S7ReadResultItem> ParseReadVarAck(ReadOnlySpan<byte> s7Pdu, int expectedCount)
    {
        if (s7Pdu.Length < 14)
        {
            throw new InvalidOperationException($"ReadVar Ack 报文过短: {s7Pdu.Length} 字节");
        }

        if (s7Pdu[0] != S7ProtocolId || s7Pdu[1] != RosctrAckData)
        {
            throw new InvalidOperationException($"非法的 ReadVar 应答帧头: ProtocolId=0x{s7Pdu[0]:X2}, ROSCTR=0x{s7Pdu[1]:X2}");
        }

        var paramLen = BinaryPrimitives.ReadUInt16BigEndian(s7Pdu.Slice(6, 2));
        var dataLen = BinaryPrimitives.ReadUInt16BigEndian(s7Pdu.Slice(8, 2));

        var errorClass = s7Pdu[10];
        var errorCode = s7Pdu[11];
        if (errorClass != 0 || errorCode != 0)
        {
            throw new InvalidOperationException($"S7 读操作失败: ErrorClass=0x{errorClass:X2}, ErrorCode=0x{errorCode:X2}");
        }

        // Data 部分起始偏移 = 12 (Header) + paramLen
        int dataOffset = 12 + paramLen;
        if (s7Pdu.Length < dataOffset + dataLen)
        {
            throw new InvalidOperationException("报文数据体长度不完整");
        }

        var results = new List<S7ReadResultItem>(expectedCount);
        int cur = dataOffset;

        for (int i = 0; i < expectedCount; i++)
        {
            if (cur + 4 > s7Pdu.Length)
            {
                // 数据体不足
                results.Add(new S7ReadResultItem { ReturnCode = S7ReturnCode.HardwareFault });
                break;
            }

            var returnCode = (S7ReturnCode)s7Pdu[cur++];
            var transportSize = (S7ResponseTransportSize)s7Pdu[cur++];
            var lengthInBits = BinaryPrimitives.ReadUInt16BigEndian(s7Pdu.Slice(cur, 2));
            cur += 2;

            if (returnCode != S7ReturnCode.Success)
            {
                results.Add(new S7ReadResultItem
                {
                    ReturnCode = returnCode,
                    Data = [],
                    IsBit = transportSize == S7ResponseTransportSize.Bit
                });
                continue;
            }

            int byteCount = transportSize switch
            {
                S7ResponseTransportSize.Bit => 1,
                _ => lengthInBits / 8
            };

            if (cur + byteCount > s7Pdu.Length)
            {
                throw new InvalidOperationException($"数据项 {i} 的数据长度超出剩余报文范围");
            }

            var itemData = s7Pdu.Slice(cur, byteCount).ToArray();
            cur += byteCount;

            // 西门子 S7 规范: 当某项数据长度为奇数字节且后续还有其它数据项时，紧跟 1 字节对齐填充
            if (byteCount % 2 != 0 && i < expectedCount - 1)
            {
                if (cur < s7Pdu.Length && s7Pdu[cur] == 0x00)
                {
                    cur++;
                }
            }

            results.Add(new S7ReadResultItem
            {
                ReturnCode = returnCode,
                Data = itemData,
                IsBit = transportSize == S7ResponseTransportSize.Bit
            });
        }

        return results;
    }

    /// <summary>
    /// 构建 S7 Write Variable (0x05) 批量写入请求 PDU
    /// </summary>
    public static byte[] BuildWriteVarPdu(ushort pduRef, IReadOnlyList<(S7Address Address, byte[] Data)> writeItems)
    {
        if (writeItems == null || writeItems.Count == 0)
        {
            throw new ArgumentException("待写入项列表不能为空", nameof(writeItems));
        }

        var paramLen = (ushort)(2 + (writeItems.Count * 12));

        // 计算 Data 部分长度
        int dataLen = 0;
        for (int i = 0; i < writeItems.Count; i++)
        {
            var item = writeItems[i];
            dataLen += 4; // ReturnCode (1) + TransportSize (1) + BitLength (2)
            dataLen += item.Data.Length;
            // 奇数字节填充
            if (item.Data.Length % 2 != 0 && i < writeItems.Count - 1)
            {
                dataLen += 1;
            }
        }

        var totalLen = 10 + paramLen + dataLen;
        var pdu = new byte[totalLen];

        // S7 Header (10 bytes)
        pdu[0] = S7ProtocolId;
        pdu[1] = RosctrJob;
        pdu[2] = 0x00;
        pdu[3] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(4, 2), pduRef);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(6, 2), paramLen);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(8, 2), (ushort)dataLen);

        // Parameter (2 + 12 * N bytes)
        pdu[10] = FuncWriteVar;
        pdu[11] = (byte)writeItems.Count;

        int idx = 12;
        foreach (var item in writeItems)
        {
            var addr = item.Address;
            pdu[idx++] = 0x12;
            pdu[idx++] = 0x0A;
            pdu[idx++] = 0x10;

            if (addr.IsBit)
            {
                pdu[idx++] = (byte)S7TransportSize.Bit;
                BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(idx, 2), 1);
            }
            else
            {
                pdu[idx++] = (byte)S7TransportSize.Byte;
                BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(idx, 2), (ushort)item.Data.Length);
            }
            idx += 2;

            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(idx, 2), (ushort)addr.DbNumber);
            idx += 2;

            pdu[idx++] = (byte)addr.Area;

            int bitOffset = addr.S7ByteBitOffset;
            pdu[idx++] = (byte)((bitOffset >> 16) & 0xFF);
            pdu[idx++] = (byte)((bitOffset >> 8) & 0xFF);
            pdu[idx++] = (byte)(bitOffset & 0xFF);
        }

        // Data 负载填充
        for (int i = 0; i < writeItems.Count; i++)
        {
            var item = writeItems[i];
            var isBit = item.Address.IsBit;

            pdu[idx++] = 0x00; // Return code placeholder
            pdu[idx++] = isBit ? (byte)S7ResponseTransportSize.Bit : (byte)S7ResponseTransportSize.ByteWordDWord;

            var bitLen = isBit ? 1 : (item.Data.Length * 8);
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(idx, 2), (ushort)bitLen);
            idx += 2;

            Buffer.BlockCopy(item.Data, 0, pdu, idx, item.Data.Length);
            idx += item.Data.Length;

            if (item.Data.Length % 2 != 0 && i < writeItems.Count - 1)
            {
                pdu[idx++] = 0x00; // 对齐填充
            }
        }

        return pdu;
    }

    /// <summary>
    /// 解析 S7 Write Variable (0x05) Ack-Data 应答报文
    /// </summary>
    public static List<S7ReturnCode> ParseWriteVarAck(ReadOnlySpan<byte> s7Pdu, int expectedCount)
    {
        if (s7Pdu.Length < 12)
        {
            throw new InvalidOperationException($"WriteVar Ack 报文过短: {s7Pdu.Length} 字节");
        }

        if (s7Pdu[0] != S7ProtocolId || s7Pdu[1] != RosctrAckData)
        {
            throw new InvalidOperationException($"非法的 WriteVar 应答帧头: ProtocolId=0x{s7Pdu[0]:X2}");
        }

        var paramLen = BinaryPrimitives.ReadUInt16BigEndian(s7Pdu.Slice(6, 2));
        var dataLen = BinaryPrimitives.ReadUInt16BigEndian(s7Pdu.Slice(8, 2));

        var errorClass = s7Pdu[10];
        var errorCode = s7Pdu[11];
        if (errorClass != 0 || errorCode != 0)
        {
            throw new InvalidOperationException($"S7 写操作失败: ErrorClass=0x{errorClass:X2}, ErrorCode=0x{errorCode:X2}");
        }

        int dataOffset = 12 + paramLen;
        var results = new List<S7ReturnCode>(expectedCount);

        for (int i = 0; i < expectedCount; i++)
        {
            if (dataOffset + i < s7Pdu.Length)
            {
                results.Add((S7ReturnCode)s7Pdu[dataOffset + i]);
            }
            else
            {
                results.Add(S7ReturnCode.HardwareFault);
            }
        }

        return results;
    }
}
