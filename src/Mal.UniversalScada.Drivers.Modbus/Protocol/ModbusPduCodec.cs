using System.Buffers.Binary;
using Mal.UniversalScada.Drivers.Modbus.Enums;

namespace Mal.UniversalScada.Drivers.Modbus.Protocol;

/// <summary>
/// Modbus 协议数据单元 (PDU) 编解码器
/// 支持 FC01, FC02, FC03, FC04, FC05, FC06, FC15, FC16 以及异常码解析
/// </summary>
public static class ModbusPduCodec
{
    public const byte FcReadCoils = 0x01;
    public const byte FcReadDiscreteInputs = 0x02;
    public const byte FcReadHoldingRegisters = 0x03;
    public const byte FcReadInputRegisters = 0x04;
    public const byte FcWriteSingleCoil = 0x05;
    public const byte FcWriteSingleRegister = 0x06;
    public const byte FcWriteMultipleCoils = 0x0F;
    public const byte FcWriteMultipleRegisters = 0x10;

    /// <summary>
    /// 构建读取请求 PDU (FC 01, 02, 03, 04)
    /// 格式: [FC (1B), StartAddress (2B BE), Quantity (2B BE)] -> 5 字节
    /// </summary>
    public static byte[] BuildReadRequestPdu(ModbusRegisterType registerType, ushort startAddress, ushort quantity)
    {
        byte fc = registerType switch
        {
            ModbusRegisterType.Coil => FcReadCoils,
            ModbusRegisterType.DiscreteInput => FcReadDiscreteInputs,
            ModbusRegisterType.InputRegister => FcReadInputRegisters,
            ModbusRegisterType.HoldingRegister => FcReadHoldingRegisters,
            _ => throw new ArgumentOutOfRangeException(nameof(registerType))
        };

        var pdu = new byte[5];
        pdu[0] = fc;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), quantity);
        return pdu;
    }

    /// <summary>
    /// 构建写单个线圈 PDU (FC 05)
    /// 格式: [0x05, OutputAddress (2B BE), 0xFF00 或 0x0000 (2B BE)] -> 5 字节
    /// </summary>
    public static byte[] BuildWriteSingleCoilPdu(ushort address, bool value)
    {
        var pdu = new byte[5];
        pdu[0] = FcWriteSingleCoil;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), value ? (ushort)0xFF00 : (ushort)0x0000);
        return pdu;
    }

    /// <summary>
    /// 构建写单个寄存器 PDU (FC 06)
    /// 格式: [0x06, RegisterAddress (2B BE), RegisterValue (2B BE)] -> 5 字节
    /// </summary>
    public static byte[] BuildWriteSingleRegisterPdu(ushort address, ushort rawWord)
    {
        var pdu = new byte[5];
        pdu[0] = FcWriteSingleRegister;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), rawWord);
        return pdu;
    }

    /// <summary>
    /// 构建写多个线圈 PDU (FC 15 / 0x0F)
    /// 格式: [0x0F, StartAddress (2B), Quantity (2B), ByteCount (1B), Values (N Bytes)]
    /// </summary>
    public static byte[] BuildWriteMultipleCoilsPdu(ushort startAddress, bool[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0) throw new ArgumentException("写入线圈数组不能为空", nameof(values));

        ushort quantity = (ushort)values.Length;
        byte byteCount = (byte)((quantity + 7) / 8);

        var pdu = new byte[6 + byteCount];
        pdu[0] = FcWriteMultipleCoils;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), quantity);
        pdu[5] = byteCount;

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                var byteIdx = 6 + (i / 8);
                var bitIdx = i % 8;
                pdu[byteIdx] |= (byte)(1 << bitIdx);
            }
        }

        return pdu;
    }

    /// <summary>
    /// 构建写多个保持寄存器 PDU (FC 16 / 0x10)
    /// 格式: [0x10, StartAddress (2B), Quantity (2B), ByteCount (1B), RegisterData (Quantity * 2 Bytes)]
    /// </summary>
    public static byte[] BuildWriteMultipleRegistersPdu(ushort startAddress, ReadOnlySpan<byte> registerBytes)
    {
        if (registerBytes.Length % 2 != 0 || registerBytes.Length == 0)
        {
            throw new ArgumentException("写入寄存器字节数必须为非零偶数", nameof(registerBytes));
        }

        ushort quantity = (ushort)(registerBytes.Length / 2);
        byte byteCount = (byte)registerBytes.Length;

        var pdu = new byte[6 + byteCount];
        pdu[0] = FcWriteMultipleRegisters;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), quantity);
        pdu[5] = byteCount;

        registerBytes.CopyTo(pdu.AsSpan(6));
        return pdu;
    }

    /// <summary>
    /// 解析读取响应 PDU (FC 01, 02, 03, 04)
    /// </summary>
    public static ReadOnlyMemory<byte> ParseReadResponse(ReadOnlySpan<byte> pdu, byte expectedFc)
    {
        CheckPduLengthAndException(pdu, expectedFc);

        if (pdu.Length < 2)
        {
            throw new InvalidOperationException("Modbus 读应答 PDU 长度过短");
        }

        var byteCount = pdu[1];
        if (pdu.Length < 2 + byteCount)
        {
            throw new InvalidOperationException($"Modbus 读应答实际长度 ({pdu.Length}) 小于标称字节数 ({2 + byteCount})");
        }

        return pdu.Slice(2, byteCount).ToArray();
    }

    /// <summary>
    /// 校验单写响应 (FC 05 / 06)
    /// </summary>
    public static void ValidateWriteSingleResponse(ReadOnlySpan<byte> pdu, byte expectedFc, ushort expectedAddress)
    {
        CheckPduLengthAndException(pdu, expectedFc);

        if (pdu.Length < 5)
        {
            throw new InvalidOperationException("Modbus 单写应答 PDU 长度不足");
        }

        var respAddress = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(1, 2));
        if (respAddress != expectedAddress)
        {
            throw new InvalidOperationException($"Modbus 单写应答地址不匹配: 期望 0x{expectedAddress:X4}, 收到 0x{respAddress:X4}");
        }
    }

    /// <summary>
    /// 校验多写响应 (FC 15 / 16)
    /// </summary>
    public static void ValidateWriteMultipleResponse(ReadOnlySpan<byte> pdu, byte expectedFc, ushort expectedAddress, ushort expectedQuantity)
    {
        CheckPduLengthAndException(pdu, expectedFc);

        if (pdu.Length < 5)
        {
            throw new InvalidOperationException("Modbus 批量写应答 PDU 长度不足");
        }

        var respAddress = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(1, 2));
        var respQuantity = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(3, 2));

        if (respAddress != expectedAddress || respQuantity != expectedQuantity)
        {
            throw new InvalidOperationException(
                $"Modbus 批量写应答不匹配: 期望起始 0x{expectedAddress:X4} 数量 {expectedQuantity}, 收到 0x{respAddress:X4} 数量 {respQuantity}");
        }
    }

    /// <summary>
    /// 检查异常码响应与基础长度
    /// </summary>
    private static void CheckPduLengthAndException(ReadOnlySpan<byte> pdu, byte expectedFc)
    {
        if (pdu.Length == 0)
        {
            throw new InvalidOperationException("收到的 Modbus PDU 为空");
        }

        var fc = pdu[0];

        // 异常响应: 最高位置 1 (FC >= 0x80)
        if ((fc & 0x80) != 0)
        {
            var errCode = pdu.Length > 1 ? pdu[1] : (byte)0;
            var errDesc = GetExceptionDescription(errCode);
            throw new ModbusException(errCode, $"从站返回 Modbus 异常响应: 功能码 0x{fc:X2}, 异常码 0x{errCode:X2} ({errDesc})");
        }

        if (fc != expectedFc)
        {
            throw new InvalidOperationException($"Modbus 功能码不匹配: 期望 0x{expectedFc:X2}, 收到 0x{fc:X2}");
        }
    }

    /// <summary>
    /// 获取 Modbus 标准异常码描述
    /// </summary>
    public static string GetExceptionDescription(byte exceptionCode) => exceptionCode switch
    {
        0x01 => "非法功能码 (Illegal Function)",
        0x02 => "非法数据地址 (Illegal Data Address)",
        0x03 => "非法数据值 (Illegal Data Value)",
        0x04 => "从站设备故障 (Slave Device Failure)",
        0x05 => "确认 (Acknowledge - 需长时间处理)",
        0x06 => "从站设备忙 (Slave Device Busy)",
        0x08 => "存储奇偶性差错 (Memory Parity Error)",
        0x0A => "网关路径不可用 (Gateway Path Unavailable)",
        0x0B => "网关目标设备无应答 (Gateway Target Device Failed to Respond)",
        _ => $"未知 Modbus 异常代码 (0x{exceptionCode:X2})"
    };
}

/// <summary>
/// Modbus 协议异常类型
/// </summary>
public class ModbusException : Exception
{
    public byte ExceptionCode { get; }

    public ModbusException(byte exceptionCode, string message) : base(message)
    {
        ExceptionCode = exceptionCode;
    }
}
