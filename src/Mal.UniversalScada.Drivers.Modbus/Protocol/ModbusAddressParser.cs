using System.Globalization;
using System.Text.RegularExpressions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Drivers.Modbus.Enums;
using Mal.UniversalScada.Drivers.Modbus.Models;

namespace Mal.UniversalScada.Drivers.Modbus.Protocol;

/// <summary>
/// 工业 Modbus 地址解析器
/// 全面支持经典 5位/6位、前缀标识 (0x/1x/3x/4x/HR/IR/C/DI)、及寄存器内位寻址 (如 40001.5)
/// </summary>
public static class ModbusAddressParser
{
    private static readonly Regex BitAddressRegex = new(
        @"^(.+)\.([0-9]{1,2})$", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// 解析点位地址字符串为结构化 ModbusAddress 对象
    /// </summary>
    /// <param name="addressString">地址文本 (如 "40001", "40001.0", "4x0001", "HR100", "00001", "C10")</param>
    /// <param name="dataType">数据类型 (确定需占用的寄存器/位数量)</param>
    /// <param name="zeroBased">是否按 0 基准偏移 (默认 false，即 40001 对应寄存器偏移 0)</param>
    public static ModbusAddress Parse(string addressString, TagDataType dataType, bool zeroBased = false)
    {
        if (string.IsNullOrWhiteSpace(addressString))
        {
            throw new ArgumentException("Modbus 地址不能为空", nameof(addressString));
        }

        var trimmed = addressString.Trim();
        int? bitIndex = null;

        // 1. 检查是否存在寄存器内位寻址 (如 40001.5, HR10.0, 4x0001.15)
        var bitMatch = BitAddressRegex.Match(trimmed);
        if (bitMatch.Success)
        {
            trimmed = bitMatch.Groups[1].Value.Trim();
            if (int.TryParse(bitMatch.Groups[2].Value, out var bit) && bit is >= 0 and <= 15)
            {
                bitIndex = bit;
            }
            else
            {
                throw new FormatException($"非法的 Modbus 寄存器位索引: {bitMatch.Groups[2].Value} (允许范围 0 ~ 15)");
            }
        }

        // 2. 解析寄存器类型与起始偏移
        var (regType, startAddr) = ParseBaseAddress(trimmed, zeroBased);

        // 如果在原生位区域 (Coil / DiscreteInput) 上写了 .bit，例如 00001.5，这在逻辑上是不合理的，抛出异常或提示
        if (bitIndex.HasValue && regType is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput)
        {
            throw aerialException($"原生位数据区 ({regType}) 不支持内部位索引 (.bit 语法)");
        }

        // 3. 根据数据类型计算占用寄存器数量
        var regCount = CalculateRegisterCount(regType, dataType, bitIndex.HasValue);

        return new ModbusAddress
        {
            RegisterType = regType,
            StartAddress = startAddr,
            RegisterCount = regCount,
            BitIndex = bitIndex,
            DataType = dataType,
            RawAddress = addressString
        };
    }

    private static (ModbusRegisterType Type, ushort Address) ParseBaseAddress(string raw, bool zeroBased)
    {
        // 模式 A: 字母前缀标识
        // HR / holding -> HoldingRegister (4x)
        if (StartsWithPrefix(raw, "HR", out var hrRemain) || StartsWithPrefix(raw, "HOLDING", out hrRemain))
        {
            return (ModbusRegisterType.HoldingRegister, ParseNumericOffset(hrRemain, zeroBased));
        }

        // IR / input -> InputRegister (3x)
        if (StartsWithPrefix(raw, "IR", out var irRemain) || StartsWithPrefix(raw, "INPUT", out irRemain))
        {
            return (ModbusRegisterType.InputRegister, ParseNumericOffset(irRemain, zeroBased));
        }

        // C / coil -> Coil (0x)
        if (StartsWithPrefix(raw, "C", out var cRemain) || StartsWithPrefix(raw, "COIL", out cRemain))
        {
            return (ModbusRegisterType.Coil, ParseNumericOffset(cRemain, zeroBased));
        }

        // DI / discrete -> DiscreteInput (1x)
        if (StartsWithPrefix(raw, "DI", out var diRemain) || StartsWithPrefix(raw, "DISCRETE", out diRemain))
        {
            return (ModbusRegisterType.DiscreteInput, ParseNumericOffset(diRemain, zeroBased));
        }

        // 模式 B: 0x, 1x, 3x, 4x 工业前缀标识
        if (raw.Length >= 3 && raw[1] is 'x' or 'X')
        {
            var prefixChar = raw[0];
            var remainStr = raw[2..].TrimStart(':');
            var offset = ParseNumericOffset(remainStr, zeroBased);

            return prefixChar switch
            {
                '0' => (ModbusRegisterType.Coil, offset),
                '1' => (ModbusRegisterType.DiscreteInput, offset),
                '3' => (ModbusRegisterType.InputRegister, offset),
                '4' => (ModbusRegisterType.HoldingRegister, offset),
                _ => throw new FormatException($"未知的 Modbus 前缀标识: {raw}")
            };
        }

        // 模式 C: 经典纯数字 5位 或 6位 寻址 (如 40001, 30001, 10001, 00001, 400001 等)
        if (ulong.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var num))
        {
            // 6 位地址区间判定
            if (raw.Length == 6)
            {
                if (num is >= 400000 and <= 465536)
                {
                    var offset = (ushort)(zeroBased ? (num - 400000) : (num > 400000 ? num - 400001 : 0));
                    return (ModbusRegisterType.HoldingRegister, offset);
                }
                if (num is >= 300000 and <= 365536)
                {
                    var offset = (ushort)(zeroBased ? (num - 300000) : (num > 300000 ? num - 300001 : 0));
                    return (ModbusRegisterType.InputRegister, offset);
                }
                if (num is >= 100000 and <= 165536)
                {
                    var offset = (ushort)(zeroBased ? (num - 100000) : (num > 100000 ? num - 100001 : 0));
                    return (ModbusRegisterType.DiscreteInput, offset);
                }
                if (num is <= 65536)
                {
                    var offset = (ushort)(zeroBased ? num : (num > 0 ? num - 1 : 0));
                    return (ModbusRegisterType.Coil, offset);
                }
            }
            // 5 位地址区间判定
            else if (raw.Length == 5)
            {
                if (num is >= 40000 and <= 49999)
                {
                    var offset = (ushort)(zeroBased ? (num - 40000) : (num > 40000 ? num - 40001 : 0));
                    return (ModbusRegisterType.HoldingRegister, offset);
                }
                if (num is >= 30000 and <= 39999)
                {
                    var offset = (ushort)(zeroBased ? (num - 30000) : (num > 30000 ? num - 30001 : 0));
                    return (ModbusRegisterType.InputRegister, offset);
                }
                if (num is >= 10000 and <= 19999)
                {
                    var offset = (ushort)(zeroBased ? (num - 10000) : (num > 10000 ? num - 10001 : 0));
                    return (ModbusRegisterType.DiscreteInput, offset);
                }
                if (num <= 9999)
                {
                    var offset = (ushort)(zeroBased ? num : (num > 0 ? num - 1 : 0));
                    return (ModbusRegisterType.Coil, offset);
                }
            }
            else
            {
                // 小于 5 位的纯数字 (如 "1", "100")，默认作为保持寄存器 0 偏移或从 1 开始
                var offset = (ushort)(zeroBased ? num : (num > 0 ? num - 1 : 0));
                return (ModbusRegisterType.HoldingRegister, offset);
            }
        }

        throw new FormatException($"无法解析的 Modbus 地址格式: '{raw}'。请使用标准地址如 40001, 30001, 10001, 00001, 4x0001 或 HR0。");
    }

    private static bool StartsWithPrefix(string input, string prefix, out string remaining)
    {
        if (input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            remaining = input[prefix.Length..].TrimStart(':', '_', '-');
            return true;
        }
        remaining = string.Empty;
        return false;
    }

    private static ushort ParseNumericOffset(string numStr, bool zeroBased)
    {
        if (string.IsNullOrWhiteSpace(numStr))
        {
            return 0;
        }

        if (ushort.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
        {
            // 如果用户写的是 HR0 或 4x0，0 始终代表物理偏移 0
            if (val == 0) return 0;

            // 若不是 zeroBased 且数字大于 0，通常按 1-based (如 HR1 对应偏移 0，HR2 对应 1)
            // 但是如果用户写了形如 HR40001，则减去 40001
            if (val >= 40001) return (ushort)(val - 40001);
            if (val >= 30001) return (ushort)(val - 30001);
            if (val >= 10001) return (ushort)(val - 10001);

            return zeroBased ? val : (ushort)(val - 1);
        }

        throw new FormatException($"Modbus 偏移量非有效整数: '{numStr}'");
    }

    private static ushort CalculateRegisterCount(ModbusRegisterType regType, TagDataType dataType, bool isBitInRegister)
    {
        if (regType is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput)
        {
            return 1; // 1 个位
        }

        if (isBitInRegister)
        {
            return 1; // 1 个 16 位寄存器包含目标位
        }

        return dataType switch
        {
            TagDataType.Bool => 1,
            TagDataType.Int8 => 1,
            TagDataType.UInt8 => 1,
            TagDataType.Int16 => 1,
            TagDataType.UInt16 => 1,
            TagDataType.Int32 => 2,
            TagDataType.UInt32 => 2,
            TagDataType.Float => 2,
            TagDataType.Int64 => 4,
            TagDataType.UInt64 => 4,
            TagDataType.Double => 4,
            TagDataType.String => 10,
            TagDataType.ByteArray => 10,
            _ => 1
        };
    }

    private static FormatException aerialException(string message) => new(message);
}
