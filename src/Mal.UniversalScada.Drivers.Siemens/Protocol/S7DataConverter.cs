using System.Buffers.Binary;
using System.Text;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Drivers.Siemens.Protocol;

/// <summary>
/// 西门子大端序 (Big-Endian) 字节流与点位工程量数值的双向转换器
/// </summary>
public static class S7DataConverter
{
    /// <summary>
    /// 从 PLC 返回的原始字节切片中解析出点位值，并应用工程量换算比例与偏移
    /// </summary>
    /// <param name="buffer">原始字节数组</param>
    /// <param name="offset">切片起始偏移</param>
    /// <param name="tag">点位元数据定义</param>
    /// <param name="isBitResult">是否为直接按位请求返回的单个布尔结果</param>
    /// <param name="bitIndex">若从整字节中提取特定位时的位索引 (0~7)</param>
    /// <returns>换算后的工程量值与原始采集值</returns>
    public static (object? Value, object? RawValue) DecodeValue(
        byte[] buffer, 
        int offset, 
        TagNode tag, 
        bool isBitResult = false, 
        byte bitIndex = 0)
    {
        var span = buffer.AsSpan(offset);
        object raw;

        switch (tag.DataType)
        {
            case TagDataType.Bool:
                if (isBitResult)
                {
                    // S7 按位读取返回的数据通常最低位为目标位
                    raw = (span[0] & 0x01) != 0;
                }
                else
                {
                    // 从字节中按 bitIndex 提取位
                    raw = ((span[0] >> bitIndex) & 0x01) != 0;
                }
                return (raw, raw);

            case TagDataType.Int8:
                raw = (sbyte)span[0];
                break;

            case TagDataType.UInt8:
                raw = span[0];
                break;

            case TagDataType.Int16:
                raw = BinaryPrimitives.ReadInt16BigEndian(span);
                break;

            case TagDataType.UInt16:
                raw = BinaryPrimitives.ReadUInt16BigEndian(span);
                break;

            case TagDataType.Int32:
                raw = BinaryPrimitives.ReadInt32BigEndian(span);
                break;

            case TagDataType.UInt32:
                raw = BinaryPrimitives.ReadUInt32BigEndian(span);
                break;

            case TagDataType.Int64:
                raw = BinaryPrimitives.ReadInt64BigEndian(span);
                break;

            case TagDataType.UInt64:
                raw = BinaryPrimitives.ReadUInt64BigEndian(span);
                break;

            case TagDataType.Float:
                raw = BinaryPrimitives.ReadSingleBigEndian(span);
                break;

            case TagDataType.Double:
                raw = BinaryPrimitives.ReadDoubleBigEndian(span);
                break;

            case TagDataType.String:
                // 西门子 S7 经典 String 格式: Byte 0 = MaxLength, Byte 1 = ActualLength, Byte 2.. = Content
                if (span.Length >= 2)
                {
                    byte actualLen = span[1];
                    int contentLen = Math.Min((int)actualLen, span.Length - 2);
                    raw = Encoding.ASCII.GetString(span.Slice(2, contentLen));
                }
                else
                {
                    raw = string.Empty;
                }
                return (raw, raw);

            case TagDataType.ByteArray:
                raw = span.ToArray();
                return (raw, raw);

            default:
                raw = BinaryPrimitives.ReadInt16BigEndian(span);
                break;
        }

        // 工程量换算: y = k * x + b
        var scaled = ApplyScaleAndOffset(raw, tag.ScaleFactor, tag.Offset);
        return (scaled, raw);
    }

    /// <summary>
    /// 将上位机准备下发的工程量数值逆换算并编码为西门子大端序 (Big-Endian) 字节流
    /// </summary>
    public static byte[] EncodeValue(object targetValue, TagNode tag)
    {
        // 1. 布尔量单点
        if (tag.DataType == TagDataType.Bool)
        {
            var bVal = Convert.ToBoolean(targetValue);
            return [bVal ? (byte)0x01 : (byte)0x00];
        }

        // 2. 逆向工程量换算: x = (y - b) / k
        var rawVal = ReverseScaleAndOffset(targetValue, tag.ScaleFactor, tag.Offset);

        switch (tag.DataType)
        {
            case TagDataType.Int8:
                return [(byte)Convert.ToSByte(rawVal)];

            case TagDataType.UInt8:
                return [Convert.ToByte(rawVal)];

            case TagDataType.Int16:
            {
                var buf = new byte[2];
                BinaryPrimitives.WriteInt16BigEndian(buf, Convert.ToInt16(rawVal));
                return buf;
            }

            case TagDataType.UInt16:
            {
                var buf = new byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(buf, Convert.ToUInt16(rawVal));
                return buf;
            }

            case TagDataType.Int32:
            {
                var buf = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(buf, Convert.ToInt32(rawVal));
                return buf;
            }

            case TagDataType.UInt32:
            {
                var buf = new byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(buf, Convert.ToUInt32(rawVal));
                return buf;
            }

            case TagDataType.Int64:
            {
                var buf = new byte[8];
                BinaryPrimitives.WriteInt64BigEndian(buf, Convert.ToInt64(rawVal));
                return buf;
            }

            case TagDataType.UInt64:
            {
                var buf = new byte[8];
                BinaryPrimitives.WriteUInt64BigEndian(buf, Convert.ToUInt64(rawVal));
                return buf;
            }

            case TagDataType.Float:
            {
                var buf = new byte[4];
                BinaryPrimitives.WriteSingleBigEndian(buf, Convert.ToSingle(rawVal));
                return buf;
            }

            case TagDataType.Double:
            {
                var buf = new byte[8];
                BinaryPrimitives.WriteDoubleBigEndian(buf, Convert.ToDouble(rawVal));
                return buf;
            }

            case TagDataType.String:
            {
                var str = targetValue.ToString() ?? string.Empty;
                var asciiBytes = Encoding.ASCII.GetBytes(str);
                // 构造标准 S7 String 头: [MaxLength, ActualLength, ...bytes]
                var s7String = new byte[2 + asciiBytes.Length];
                s7String[0] = (byte)Math.Min(254, asciiBytes.Length);
                s7String[1] = (byte)Math.Min(254, asciiBytes.Length);
                Array.Copy(asciiBytes, 0, s7String, 2, s7String[1]);
                return s7String;
            }

            case TagDataType.ByteArray:
                if (targetValue is byte[] bArr) return bArr;
                return [];

            default:
            {
                var buf = new byte[2];
                BinaryPrimitives.WriteInt16BigEndian(buf, Convert.ToInt16(rawVal));
                return buf;
            }
        }
    }

    private static object ApplyScaleAndOffset(object raw, double scale, double offset)
    {
        if (Math.Abs(scale - 1.0) < 1e-9 && Math.Abs(offset) < 1e-9)
        {
            return raw;
        }

        try
        {
            var d = Convert.ToDouble(raw);
            return (d * scale) + offset;
        }
        catch
        {
            return raw;
        }
    }

    private static object ReverseScaleAndOffset(object val, double scale, double offset)
    {
        if (Math.Abs(scale - 1.0) < 1e-9 && Math.Abs(offset) < 1e-9)
        {
            return val;
        }

        try
        {
            if (Math.Abs(scale) < 1e-9) return val;
            var d = Convert.ToDouble(val);
            return (d - offset) / scale;
        }
        catch
        {
            return val;
        }
    }
}
