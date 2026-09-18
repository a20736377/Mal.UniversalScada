using System.Globalization;
using System.Text.RegularExpressions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Drivers.CustomSerial.Models;

namespace Mal.UniversalScada.Drivers.CustomSerial.Protocol;

/// <summary>
/// 串口私有协议点位地址解析器
/// 支持 CMDxx.OFFSETxx、REGxx、INDEXxx (ASCII 文本列) 以及位寻址 (.bit)
/// </summary>
public static class CustomSerialAddressParser
{
    private static readonly Regex CmdRegex = new(
        @"^(?:CMD|FC|OP)?([0-9a-fA-F]{1,2})\.([0-9]+)(?:\.([0-9]{1,2}))?$", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RegRegex = new(
        @"^(?:REG|D|W)([0-9]+)(?:\.([0-9]{1,2}))?$", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FieldRegex = new(
        @"^(?:INDEX|FIELD|COL|CSV)([0-9]+)$", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// 解析点位地址文本
    /// </summary>
    public static CustomSerialAddress Parse(string addressString, TagDataType dataType)
    {
        if (string.IsNullOrWhiteSpace(addressString))
        {
            throw new ArgumentException("点位地址不能为空", nameof(addressString));
        }

        var trimmed = addressString.Trim();

        // 1. 匹配 ASCII 文本模式索引 (如 INDEX0, FIELD1, COL2)
        var fieldMatch = FieldRegex.Match(trimmed);
        if (fieldMatch.Success)
        {
            int fieldIdx = int.Parse(fieldMatch.Groups[1].Value);
            return new CustomSerialAddress
            {
                Command = 0x01,
                ByteOffset = 0,
                FieldIndex = fieldIdx,
                DataType = dataType,
                RawAddress = addressString
            };
        }

        // 2. 匹配标准指令与偏移 (如 CMD01.0, 01.4, CMD03.2.5)
        var cmdMatch = CmdRegex.Match(trimmed);
        if (cmdMatch.Success)
        {
            byte cmd = byte.Parse(cmdMatch.Groups[1].Value, NumberStyles.HexNumber);
            int offset = int.Parse(cmdMatch.Groups[2].Value);
            int? bit = null;
            if (cmdMatch.Groups[3].Success)
            {
                bit = int.Parse(cmdMatch.Groups[3].Value);
            }

            return new CustomSerialAddress
            {
                Command = cmd,
                ByteOffset = offset,
                BitIndex = bit,
                DataType = dataType,
                RawAddress = addressString
            };
        }

        // 3. 匹配寄存器别名 (如 REG0, D10, W0)
        var regMatch = RegRegex.Match(trimmed);
        if (regMatch.Success)
        {
            int regIndex = int.Parse(regMatch.Groups[1].Value);
            int? bit = null;
            if (regMatch.Groups[2].Success)
            {
                bit = int.Parse(regMatch.Groups[2].Value);
            }

            return new CustomSerialAddress
            {
                Command = 0x01,
                ByteOffset = regIndex * 2,
                BitIndex = bit,
                DataType = dataType,
                RawAddress = addressString
            };
        }

        // 4. 纯数字直接解析为偏移 (如 "0", "2", "4")
        if (int.TryParse(trimmed, out var numOffset))
        {
            return new CustomSerialAddress
            {
                Command = 0x01,
                ByteOffset = numOffset,
                DataType = dataType,
                RawAddress = addressString
            };
        }

        throw new FormatException($"未知的串口点位地址格式: '{addressString}'。请使用如 CMD01.0, REG0 或 INDEX0。");
    }
}
