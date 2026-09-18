using System.Buffers.Binary;
using System.Text;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.Modbus.Enums;

namespace Mal.UniversalScada.Drivers.Modbus.Protocol;

/// <summary>
/// Modbus 原始字节与上层工程数据类型转换器
/// 支持 ABCD, CDAB, BADC, DCBA 4 种工业字序/字节序转换以及线性工程比例缩放 (y = k * x + b)
/// </summary>
public static class ModbusDataConverter
{
    /// <summary>
    /// 将从下位机读取的原始字节解码为点位对应的工程值
    /// </summary>
    /// <param name="rawSpan">接收到的原始寄存器/位字节跨度</param>
    /// <param name="tag">点位元数据 (包含 DataType, ScaleFactor, Offset)</param>
    /// <param name="endian">字序模式</param>
    /// <param name="bitIndex">寄存器内部位索引 (若为位寻址)</param>
    /// <returns>(换算后的工程值, 未经换算的原始数值)</returns>
    public static (object? Value, object? RawValue) DecodeValue(
        ReadOnlySpan<byte> rawSpan, 
        TagNode tag, 
        ModbusEndian endian, 
        int? bitIndex = null)
    {
        if (rawSpan.IsEmpty) return (null, null);

        // 1. 寄存器内位寻址 (例如 40001.5)
        if (bitIndex.HasValue)
        {
            if (rawSpan.Length < 2) return (null, null);
            var reg = ReadUInt16(rawSpan, endian);
            var isSet = ((reg >> bitIndex.Value) & 1) == 1;
            return (isSet, isSet);
        }

        // 2. 原生布尔量 (线圈或离散输入，通常 1 字节中的第 0 位)
        if (tag.DataType == TagDataType.Bool)
        {
            var bVal = (rawSpan[0] & 1) == 1;
            return (bVal, bVal);
        }

        // 3. 各类数值类型解析
        object? rawVal = null;
        double numericVal = 0.0;
        bool isNumeric = true;

        switch (tag.DataType)
        {
            case TagDataType.Int8:
                sbyte sb = (sbyte)rawSpan[0];
                rawVal = sb;
                numericVal = sb;
                break;

            case TagDataType.UInt8:
                byte ub = rawSpan[0];
                rawVal = ub;
                numericVal = ub;
                break;

            case TagDataType.Int16:
                if (rawSpan.Length < 2) return (null, null);
                short s = ReadInt16(rawSpan, endian);
                rawVal = s;
                numericVal = s;
                break;

            case TagDataType.UInt16:
                if (rawSpan.Length < 2) return (null, null);
                ushort us = ReadUInt16(rawSpan, endian);
                rawVal = us;
                numericVal = us;
                break;

            case TagDataType.Int32:
                if (rawSpan.Length < 4) return (null, null);
                int i32 = ReadInt32(rawSpan, endian);
                rawVal = i32;
                numericVal = i32;
                break;

            case TagDataType.UInt32:
                if (rawSpan.Length < 4) return (null, null);
                uint ui32 = ReadUInt32(rawSpan, endian);
                rawVal = ui32;
                numericVal = ui32;
                break;

            case TagDataType.Float:
                if (rawSpan.Length < 4) return (null, null);
                float f = ReadFloat(rawSpan, endian);
                rawVal = f;
                numericVal = f;
                break;

            case TagDataType.Int64:
                if (rawSpan.Length < 8) return (null, null);
                long i64 = ReadInt64(rawSpan, endian);
                rawVal = i64;
                numericVal = i64;
                break;

            case TagDataType.UInt64:
                if (rawSpan.Length < 8) return (null, null);
                ulong ui64 = ReadUInt64(rawSpan, endian);
                rawVal = ui64;
                numericVal = ui64;
                break;

            case TagDataType.Double:
                if (rawSpan.Length < 8) return (null, null);
                double d = ReadDouble(rawSpan, endian);
                rawVal = d;
                numericVal = d;
                break;

            case TagDataType.String:
                isNumeric = false;
                var str = Encoding.ASCII.GetString(rawSpan).TrimEnd('\0');
                return (str, str);

            case TagDataType.ByteArray:
                isNumeric = false;
                var arr = rawSpan.ToArray();
                return (arr, arr);

            default:
                if (rawSpan.Length >= 2)
                {
                    ushort defVal = ReadUInt16(rawSpan, endian);
                    rawVal = defVal;
                    numericVal = defVal;
                }
                break;
        }

        if (!isNumeric || rawVal == null)
        {
            return (rawVal, rawVal);
        }

        // 4. 线性工程比例变换: y = k * x + b
        // 若没有配置比例换算 (scale=1.0, offset=0.0)，直接返回原生强类型 (short, int, float 等)
        if (Math.Abs(tag.ScaleFactor - 1.0) < 1e-9 && Math.Abs(tag.Offset) < 1e-9)
        {
            return (rawVal, rawVal);
        }

        var scaled = ApplyScaling(numericVal, tag.ScaleFactor, tag.Offset);
        return (scaled, rawVal);
    }

    /// <summary>
    /// 将上位机下发的工程值反向编码为 Modbus 寄存器原始字节序列
    /// </summary>
    public static byte[] EncodeValue(
        object value, 
        TagNode tag, 
        ModbusEndian endian, 
        int? bitIndex = null)
    {
        // 1. 布尔量
        if (tag.DataType == TagDataType.Bool)
        {
            var b = Convert.ToBoolean(value);
            return b ? [0x01] : [0x00];
        }

        // 2. 数值类型执行逆工程比例换算: x = (y - b) / k
        double engVal = Convert.ToDouble(value);
        double rawVal = ReverseScaling(engVal, tag.ScaleFactor, tag.Offset);

        switch (tag.DataType)
        {
            case TagDataType.Int8:
                return [(byte)(sbyte)Math.Round(rawVal)];

            case TagDataType.UInt8:
                return [(byte)Math.Round(rawVal)];

            case TagDataType.Int16:
                return WriteInt16((short)Math.Round(rawVal), endian);

            case TagDataType.UInt16:
                return WriteUInt16((ushort)Math.Round(rawVal), endian);

            case TagDataType.Int32:
                return WriteInt32((int)Math.Round(rawVal), endian);

            case TagDataType.UInt32:
                return WriteUInt32((uint)Math.Round(rawVal), endian);

            case TagDataType.Float:
                return WriteFloat((float)rawVal, endian);

            case TagDataType.Int64:
                return WriteInt64((long)Math.Round(rawVal), endian);

            case TagDataType.UInt64:
                return WriteUInt64((ulong)Math.Round(rawVal), endian);

            case TagDataType.Double:
                return WriteDouble(rawVal, endian);

            case TagDataType.String:
                var strBytes = Encoding.ASCII.GetBytes(value.ToString() ?? string.Empty);
                if (strBytes.Length % 2 != 0)
                {
                    Array.Resize(ref strBytes, strBytes.Length + 1);
                }
                return strBytes;

            case TagDataType.ByteArray:
                if (value is byte[] bytes) return bytes;
                return [];

            default:
                return WriteUInt16((ushort)Math.Round(rawVal), endian);
        }
    }

    #region 字节序重排核心算法 (ABCD, CDAB, BADC, DCBA)

    public static short ReadInt16(ReadOnlySpan<byte> span, ModbusEndian endian)
    {
        return (short)ReadUInt16(span, endian);
    }

    public static ushort ReadUInt16(ReadOnlySpan<byte> span, ModbusEndian endian)
    {
        // 16 位 2 字节: A, B
        return endian switch
        {
            ModbusEndian.BADC or ModbusEndian.DCBA => 
                (ushort)((span[1] << 8) | span[0]), // 字节交换
            _ => 
                (ushort)((span[0] << 8) | span[1])  // 标准大端序 A, B
        };
    }

    public static byte[] WriteUInt16(ushort val, ModbusEndian endian)
    {
        var b0 = (byte)((val >> 8) & 0xFF);
        var b1 = (byte)(val & 0xFF);

        return endian switch
        {
            ModbusEndian.BADC or ModbusEndian.DCBA => [b1, b0],
            _ => [b0, b1]
        };
    }

    public static byte[] WriteInt16(short val, ModbusEndian endian) => WriteUInt16((ushort)val, endian);

    public static int ReadInt32(ReadOnlySpan<byte> span, ModbusEndian endian)
    {
        return (int)ReadUInt32(span, endian);
    }

    public static uint ReadUInt32(ReadOnlySpan<byte> span, ModbusEndian endian)
    {
        Span<byte> ordered = stackalloc byte[4];
        Reorder32(span, ordered, endian);
        return BinaryPrimitives.ReadUInt32BigEndian(ordered);
    }

    public static float ReadFloat(ReadOnlySpan<byte> span, ModbusEndian endian)
    {
        Span<byte> ordered = stackalloc byte[4];
        Reorder32(span, ordered, endian);
        return BinaryPrimitives.ReadSingleBigEndian(ordered);
    }

    public static byte[] WriteUInt32(uint val, ModbusEndian endian)
    {
        Span<byte> be = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(be, val);
        var res = new byte[4];
        InverseReorder32(be, res, endian);
        return res;
    }

    public static byte[] WriteInt32(int val, ModbusEndian endian) => WriteUInt32((uint)val, endian);

    public static byte[] WriteFloat(float val, ModbusEndian endian)
    {
        Span<byte> be = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(be, val);
        var res = new byte[4];
        InverseReorder32(be, res, endian);
        return res;
    }

    private static void Reorder32(ReadOnlySpan<byte> src, Span<byte> dst, ModbusEndian endian)
    {
        // src 原始线序字节: [0]=A, [1]=B, [2]=C, [3]=D
        switch (endian)
        {
            case ModbusEndian.ABCD: // 标准大端
                dst[0] = src[0]; dst[1] = src[1]; dst[2] = src[2]; dst[3] = src[3];
                break;
            case ModbusEndian.CDAB: // 字交换
                dst[0] = src[2]; dst[1] = src[3]; dst[2] = src[0]; dst[3] = src[1];
                break;
            case ModbusEndian.BADC: // 字节交换
                dst[0] = src[1]; dst[1] = src[0]; dst[2] = src[3]; dst[3] = src[2];
                break;
            case ModbusEndian.DCBA: // 小端序
                dst[0] = src[3]; dst[1] = src[2]; dst[2] = src[1]; dst[3] = src[0];
                break;
        }
    }

    private static void InverseReorder32(ReadOnlySpan<byte> be, Span<byte> dst, ModbusEndian endian)
    {
        // be 是标准的 [A, B, C, D]，按指定端序重排后发往下位机
        switch (endian)
        {
            case ModbusEndian.ABCD:
                dst[0] = be[0]; dst[1] = be[1]; dst[2] = be[2]; dst[3] = be[3];
                break;
            case ModbusEndian.CDAB:
                dst[0] = be[2]; dst[1] = be[3]; dst[2] = be[0]; dst[3] = be[1];
                break;
            case ModbusEndian.BADC:
                dst[0] = be[1]; dst[1] = be[0]; dst[2] = be[3]; dst[3] = be[2];
                break;
            case ModbusEndian.DCBA:
                dst[0] = be[3]; dst[1] = be[2]; dst[2] = be[1]; dst[3] = be[0];
                break;
        }
    }

    public static long ReadInt64(ReadOnlySpan<byte> span, ModbusEndian endian)
    {
        return (long)ReadUInt64(span, endian);
    }

    public static ulong ReadUInt64(ReadOnlySpan<byte> span, ModbusEndian endian)
    {
        Span<byte> ordered = stackalloc byte[8];
        Reorder64(span, ordered, endian);
        return BinaryPrimitives.ReadUInt64BigEndian(ordered);
    }

    public static double ReadDouble(ReadOnlySpan<byte> span, ModbusEndian endian)
    {
        Span<byte> ordered = stackalloc byte[8];
        Reorder64(span, ordered, endian);
        return BinaryPrimitives.ReadDoubleBigEndian(ordered);
    }

    public static byte[] WriteUInt64(ulong val, ModbusEndian endian)
    {
        Span<byte> be = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(be, val);
        var res = new byte[8];
        InverseReorder64(be, res, endian);
        return res;
    }

    public static byte[] WriteInt64(long val, ModbusEndian endian) => WriteUInt64((ulong)val, endian);

    public static byte[] WriteDouble(double val, ModbusEndian endian)
    {
        Span<byte> be = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(be, val);
        var res = new byte[8];
        InverseReorder64(be, res, endian);
        return res;
    }

    private static void Reorder64(ReadOnlySpan<byte> src, Span<byte> dst, ModbusEndian endian)
    {
        // 64 位 8 字节: [0..7]
        switch (endian)
        {
            case ModbusEndian.ABCD:
                src[..8].CopyTo(dst);
                break;
            case ModbusEndian.CDAB: // 4 个字反转: Word3, Word2, Word1, Word0
                dst[0] = src[6]; dst[1] = src[7];
                dst[2] = src[4]; dst[3] = src[5];
                dst[4] = src[2]; dst[5] = src[3];
                dst[6] = src[0]; dst[7] = src[1];
                break;
            case ModbusEndian.BADC: // 每个字内字节反转
                for (int i = 0; i < 8; i += 2)
                {
                    dst[i] = src[i + 1];
                    dst[i + 1] = src[i];
                }
                break;
            case ModbusEndian.DCBA: // 完全纯小端
                for (int i = 0; i < 8; i++)
                {
                    dst[i] = src[7 - i];
                }
                break;
        }
    }

    private static void InverseReorder64(ReadOnlySpan<byte> be, Span<byte> dst, ModbusEndian endian)
    {
        switch (endian)
        {
            case ModbusEndian.ABCD:
                be[..8].CopyTo(dst);
                break;
            case ModbusEndian.CDAB:
                dst[0] = be[6]; dst[1] = be[7];
                dst[2] = be[4]; dst[3] = be[5];
                dst[4] = be[2]; dst[5] = be[3];
                dst[6] = be[0]; dst[7] = be[1];
                break;
            case ModbusEndian.BADC:
                for (int i = 0; i < 8; i += 2)
                {
                    dst[i] = be[i + 1];
                    dst[i + 1] = be[i];
                }
                break;
            case ModbusEndian.DCBA:
                for (int i = 0; i < 8; i++)
                {
                    dst[i] = be[7 - i];
                }
                break;
        }
    }

    #endregion

    private static double ApplyScaling(double raw, double scaleFactor, double offset)
    {
        if (Math.Abs(scaleFactor - 1.0) < 1e-9 && Math.Abs(offset) < 1e-9)
        {
            return raw;
        }
        return raw * scaleFactor + offset;
    }

    private static double ReverseScaling(double eng, double scaleFactor, double offset)
    {
        if (Math.Abs(scaleFactor) < 1e-9)
        {
            return eng - offset;
        }
        return (eng - offset) / scaleFactor;
    }

    private static object CastToOriginalType(double val, TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Int8 => (sbyte)Math.Round(val),
            TagDataType.UInt8 => (byte)Math.Round(val),
            TagDataType.Int16 => (short)Math.Round(val),
            TagDataType.UInt16 => (ushort)Math.Round(val),
            TagDataType.Int32 => (int)Math.Round(val),
            TagDataType.UInt32 => (uint)Math.Round(val),
            TagDataType.Float => (float)val,
            TagDataType.Int64 => (long)Math.Round(val),
            TagDataType.UInt64 => (ulong)Math.Round(val),
            TagDataType.Double => val,
            _ => val
        };
    }
}
