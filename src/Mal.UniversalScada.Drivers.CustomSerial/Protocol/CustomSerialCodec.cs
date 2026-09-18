using System.Buffers.Binary;
using System.Text;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.CustomSerial.Enums;
using Mal.UniversalScada.Drivers.CustomSerial.Models;

namespace Mal.UniversalScada.Drivers.CustomSerial.Protocol;

/// <summary>
/// 串口私有报文编解码与解包器
/// </summary>
public static class CustomSerialCodec
{
    /// <summary>
    /// 构建发送到单片机/嵌入式设备的标准二进制请求帧
    /// 结构: [Header, Station, Cmd, Length, Payload, Checksum, Tail]
    /// </summary>
    public static byte[] BuildBinaryRequest(CustomSerialConfig config, byte cmd, ReadOnlySpan<byte> payload)
    {
        var headerLen = config.Header.Length;
        var tailLen = config.Tail.Length;
        var payloadLen = payload.Length;
        var checkLen = GetCheckLength(config.CheckType);

        // 待校验内容: [Station(1), Cmd(1), Length(1), Payload(N)]
        var bodyLen = 1 + 1 + 1 + payloadLen;
        var body = new byte[bodyLen];
        body[0] = config.StationAddress;
        body[1] = cmd;
        body[2] = (byte)payloadLen;
        if (payloadLen > 0)
        {
            payload.CopyTo(body.AsSpan(3));
        }

        var checkBytes = CustomSerialChecksum.Compute(config.CheckType, body);

        // 组装总报文
        var totalLen = headerLen + bodyLen + checkLen + tailLen;
        var frame = new byte[totalLen];

        int offset = 0;
        if (headerLen > 0)
        {
            config.Header.CopyTo(frame, offset);
            offset += headerLen;
        }

        body.CopyTo(frame, offset);
        offset += bodyLen;

        if (checkLen > 0)
        {
            checkBytes.CopyTo(frame, offset);
            offset += checkLen;
        }

        if (tailLen > 0)
        {
            config.Tail.CopyTo(frame, offset);
        }

        return frame;
    }

    /// <summary>
    /// 校验并解包下位机返回的二进制应答帧
    /// </summary>
    public static bool TryUnpackBinaryResponse(
        ReadOnlySpan<byte> frame, 
        CustomSerialConfig config, 
        out byte respCmd, 
        out byte[] payload, 
        out string? error)
    {
        respCmd = 0;
        payload = [];
        error = null;

        var headerLen = config.Header.Length;
        var tailLen = config.Tail.Length;
        var checkLen = GetCheckLength(config.CheckType);

        // 最小帧长: 帧头 + Station(1) + Cmd(1) + Len(1) + 校验 + 帧尾
        var minLen = headerLen + 3 + checkLen + tailLen;
        if (frame.Length < minLen)
        {
            error = $"报文长度不足 (实际 {frame.Length} 字节, 最小需要 {minLen} 字节)";
            return false;
        }

        // 1. 检查帧头
        if (headerLen > 0 && !frame[..headerLen].SequenceEqual(config.Header))
        {
            error = "报文帧头不匹配";
            return false;
        }

        // 2. 检查帧尾
        if (tailLen > 0 && !frame[^tailLen..].SequenceEqual(config.Tail))
        {
            error = "报文帧尾不匹配";
            return false;
        }

        // 3. 提取主体内容
        var bodySpan = frame.Slice(headerLen, frame.Length - headerLen - checkLen - tailLen);
        var respStation = bodySpan[0];
        respCmd = bodySpan[1];
        var dataLen = bodySpan[2];

        if (bodySpan.Length < 3 + dataLen)
        {
            error = $"报文载荷长度标称 {dataLen} 字节，实际仅剩余 {bodySpan.Length - 3} 字节";
            return false;
        }

        // 4. 校验和校验
        if (checkLen > 0)
        {
            var checkSpan = frame.Slice(headerLen + bodySpan.Length, checkLen);
            if (!CustomSerialChecksum.Validate(config.CheckType, bodySpan, checkSpan))
            {
                error = "报文校验和检验失败";
                return false;
            }
        }

        payload = bodySpan.Slice(3, dataLen).ToArray();
        return true;
    }

    /// <summary>
    /// 将从单片机读取的有效载荷解码为指定点位的值
    /// </summary>
    public static (object? Value, object? RawValue) DecodePayload(
        ReadOnlySpan<byte> payload, 
        CustomSerialAddress addr, 
        TagNode tag, 
        bool isBigEndian)
    {
        if (payload.Length == 0) return (null, null);

        // 1. 字节内按位寻址 (CMD01.0.5)
        if (addr.IsBit)
        {
            if (addr.ByteOffset >= payload.Length) return (null, null);
            var b = payload[addr.ByteOffset];
            bool isSet = ((b >> addr.BitIndex!.Value) & 1) == 1;
            return (isSet, isSet);
        }

        // 2. 原生布尔量
        if (tag.DataType == TagDataType.Bool)
        {
            if (addr.ByteOffset >= payload.Length) return (null, null);
            bool isSet = payload[addr.ByteOffset] != 0;
            return (isSet, isSet);
        }

        // 3. 数值类型提取
        int offset = addr.ByteOffset;
        object? raw = null;
        double num = 0.0;

        switch (tag.DataType)
        {
            case TagDataType.Int8:
                if (offset < payload.Length)
                {
                    sbyte sb = (sbyte)payload[offset];
                    raw = sb; num = sb;
                }
                break;

            case TagDataType.UInt8:
                if (offset < payload.Length)
                {
                    byte ub = payload[offset];
                    raw = ub; num = ub;
                }
                break;

            case TagDataType.Int16:
                if (offset + 2 <= payload.Length)
                {
                    short s = isBigEndian 
                        ? BinaryPrimitives.ReadInt16BigEndian(payload.Slice(offset, 2))
                        : BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(offset, 2));
                    raw = s; num = s;
                }
                break;

            case TagDataType.UInt16:
                if (offset + 2 <= payload.Length)
                {
                    ushort us = isBigEndian 
                        ? BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(offset, 2))
                        : BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset, 2));
                    raw = us; num = us;
                }
                break;

            case TagDataType.Int32:
                if (offset + 4 <= payload.Length)
                {
                    int i32 = isBigEndian 
                        ? BinaryPrimitives.ReadInt32BigEndian(payload.Slice(offset, 4))
                        : BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, 4));
                    raw = i32; num = i32;
                }
                break;

            case TagDataType.UInt32:
                if (offset + 4 <= payload.Length)
                {
                    uint ui32 = isBigEndian 
                        ? BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(offset, 4))
                        : BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(offset, 4));
                    raw = ui32; num = ui32;
                }
                break;

            case TagDataType.Float:
                if (offset + 4 <= payload.Length)
                {
                    float f = isBigEndian 
                        ? BinaryPrimitives.ReadSingleBigEndian(payload.Slice(offset, 4))
                        : BinaryPrimitives.ReadSingleLittleEndian(payload.Slice(offset, 4));
                    raw = f; num = f;
                }
                break;

            case TagDataType.Double:
                if (offset + 8 <= payload.Length)
                {
                    double d = isBigEndian 
                        ? BinaryPrimitives.ReadDoubleBigEndian(payload.Slice(offset, 8))
                        : BinaryPrimitives.ReadDoubleLittleEndian(payload.Slice(offset, 8));
                    raw = d; num = d;
                }
                break;

            case TagDataType.String:
                if (offset < payload.Length)
                {
                    var str = Encoding.ASCII.GetString(payload[offset..]).TrimEnd('\0');
                    return (str, str);
                }
                break;

            default:
                if (offset + 2 <= payload.Length)
                {
                    short defVal = isBigEndian 
                        ? BinaryPrimitives.ReadInt16BigEndian(payload.Slice(offset, 2))
                        : BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(offset, 2));
                    raw = defVal; num = defVal;
                }
                break;
        }

        if (raw == null) return (null, null);

        // 4. 线性工程换算: y = k * x + b
        if (Math.Abs(tag.ScaleFactor - 1.0) < 1e-9 && Math.Abs(tag.Offset) < 1e-9)
        {
            return (raw, raw);
        }

        var scaled = num * tag.ScaleFactor + tag.Offset;
        return (scaled, raw);
    }

    /// <summary>
    /// 解析 ASCII 文本行数据 (如 "DATA,25.4,60.2\r\n")
    /// </summary>
    public static (object? Value, object? RawValue) DecodeAsciiLine(
        string line, 
        CustomSerialAddress addr, 
        TagNode tag)
    {
        if (string.IsNullOrWhiteSpace(line)) return (null, null);

        var trimmed = line.Trim();
        var parts = trimmed.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        int idx = addr.FieldIndex ?? 0;
        if (idx >= parts.Length) return (null, null);

        var token = parts[idx];
        if (double.TryParse(token, out var num))
        {
            if (Math.Abs(tag.ScaleFactor - 1.0) < 1e-9 && Math.Abs(tag.Offset) < 1e-9)
            {
                return (num, num);
            }
            return (num * tag.ScaleFactor + tag.Offset, num);
        }

        return (token, token);
    }

    private static int GetCheckLength(CustomSerialCheckType type) => type switch
    {
        CustomSerialCheckType.Sum8 or CustomSerialCheckType.Xor8 => 1,
        CustomSerialCheckType.Crc16Modbus or CustomSerialCheckType.Crc16Ccitt => 2,
        _ => 0
    };
}
