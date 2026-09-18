using System.Text.RegularExpressions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Drivers.Siemens.Enums;
using Mal.UniversalScada.Drivers.Siemens.Models;

namespace Mal.UniversalScada.Drivers.Siemens.Protocol;

/// <summary>
/// 西门子工业地址语法解析器。
/// 支持解析 DB、M、I/E、Q/A、V 等各种区域的标准西门子地址表达式。
/// </summary>
public static class S7AddressParser
{
    // 正则表达式预编译
    // 匹配 DB 块: DB1.DBX0.0, DB1.DBD0, DB1.DBW4, DB1.DBB10, DB1.0.0, DB1.10
    private static readonly Regex DbRegex = new(
        @"^DB(?<db>\d+)\.(?:DB(?<type>[XBWD])|(?<type>[XBWD]))?(?<byte>\d+)(?:\.(?<bit>[0-7]))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 匹配 M 区: M0.0, MB1, MW2, MD4, ML8
    private static readonly Regex MerkerRegex = new(
        @"^M(?<type>[BWDX])?(?<byte>\d+)(?:\.(?<bit>[0-7]))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 匹配输入区 I/E: I0.0, IB0, IW2, ID4, E0.0, EB0, EW2, ED4
    private static readonly Regex InputRegex = new(
        @"^[IE](?<type>[BWDX])?(?<byte>\d+)(?:\.(?<bit>[0-7]))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 匹配输出区 Q/A: Q0.0, QB0, QW2, QD4, A0.0, AB0, AW2, AD4
    private static readonly Regex OutputRegex = new(
        @"^[QA](?<type>[BWDX])?(?<byte>\d+)(?:\.(?<bit>[0-7]))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 匹配 S7-200/Smart V区: V0.0, VB0, VW2, VD4 (映射为 DB1)
    private static readonly Regex VAreaRegex = new(
        @"^V(?<type>[BWDX])?(?<byte>\d+)(?:\.(?<bit>[0-7]))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 匹配定时器/计数器: T1, C1, Z1
    private static readonly Regex TimerCounterRegex = new(
        @"^(?<kind>[TCZ])(?<num>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// 解析点位配置的硬件地址字符串
    /// </summary>
    /// <param name="rawAddress">地址文本 (如 "DB1.DBD0", "M0.0", "IW0", "V100")</param>
    /// <param name="dataType">点位预设的数据类型</param>
    /// <returns>结构化西门子物理地址对象</returns>
    /// <exception cref="FormatException">当地址语法不合法时抛出异常</exception>
    public static S7Address Parse(string rawAddress, TagDataType dataType)
    {
        if (string.IsNullOrWhiteSpace(rawAddress))
        {
            throw new ArgumentException("地址字符串不能为空", nameof(rawAddress));
        }

        var clean = rawAddress.Trim().Replace(" ", "");
        var byteLen = GetByteLength(dataType);

        // 1. 尝试匹配 DB 块
        var match = DbRegex.Match(clean);
        if (match.Success)
        {
            var dbNumber = int.Parse(match.Groups["db"].Value);
            var startByte = int.Parse(match.Groups["byte"].Value);
            var typeStr = match.Groups["type"].Value.ToUpperInvariant();
            var bitGroup = match.Groups["bit"];

            var isBit = bitGroup.Success || typeStr == "X" || dataType == TagDataType.Bool;
            byte bitIndex = bitGroup.Success ? byte.Parse(bitGroup.Value) : (byte)0;

            // 若显式标注了数据类型指示符，可校验调整长度
            if (string.IsNullOrEmpty(typeStr) && !bitGroup.Success && dataType == TagDataType.Bool)
            {
                isBit = true;
            }

            return new S7Address
            {
                Area = S7AreaCode.DataBlock,
                DbNumber = dbNumber,
                StartByte = startByte,
                BitIndex = bitIndex,
                ByteLength = isBit ? 1 : byteLen,
                DataType = isBit ? TagDataType.Bool : dataType,
                RawAddress = rawAddress
            };
        }

        // 2. 尝试匹配 V 区 (S7-200/Smart 映射为 DB1)
        match = VAreaRegex.Match(clean);
        if (match.Success)
        {
            var startByte = int.Parse(match.Groups["byte"].Value);
            var typeStr = match.Groups["type"].Value.ToUpperInvariant();
            var bitGroup = match.Groups["bit"];

            var isBit = bitGroup.Success || typeStr == "X" || dataType == TagDataType.Bool;
            byte bitIndex = bitGroup.Success ? byte.Parse(bitGroup.Value) : (byte)0;

            return new S7Address
            {
                Area = S7AreaCode.DataBlock,
                DbNumber = 1, // S7-200 V 存储区固定映射为 DB 1
                StartByte = startByte,
                BitIndex = bitIndex,
                ByteLength = isBit ? 1 : byteLen,
                DataType = isBit ? TagDataType.Bool : dataType,
                RawAddress = rawAddress
            };
        }

        // 3. 尝试匹配 M 标志区
        match = MerkerRegex.Match(clean);
        if (match.Success)
        {
            var startByte = int.Parse(match.Groups["byte"].Value);
            var typeStr = match.Groups["type"].Value.ToUpperInvariant();
            var bitGroup = match.Groups["bit"];

            var isBit = bitGroup.Success || typeStr == "X" || dataType == TagDataType.Bool;
            byte bitIndex = bitGroup.Success ? byte.Parse(bitGroup.Value) : (byte)0;

            return new S7Address
            {
                Area = S7AreaCode.Merker,
                DbNumber = 0,
                StartByte = startByte,
                BitIndex = bitIndex,
                ByteLength = isBit ? 1 : byteLen,
                DataType = isBit ? TagDataType.Bool : dataType,
                RawAddress = rawAddress
            };
        }

        // 4. 尝试匹配 I/E 输入区
        match = InputRegex.Match(clean);
        if (match.Success)
        {
            var startByte = int.Parse(match.Groups["byte"].Value);
            var typeStr = match.Groups["type"].Value.ToUpperInvariant();
            var bitGroup = match.Groups["bit"];

            var isBit = bitGroup.Success || typeStr == "X" || dataType == TagDataType.Bool;
            byte bitIndex = bitGroup.Success ? byte.Parse(bitGroup.Value) : (byte)0;

            return new S7Address
            {
                Area = S7AreaCode.Inputs,
                DbNumber = 0,
                StartByte = startByte,
                BitIndex = bitIndex,
                ByteLength = isBit ? 1 : byteLen,
                DataType = isBit ? TagDataType.Bool : dataType,
                RawAddress = rawAddress
            };
        }

        // 5. 尝试匹配 Q/A 输出区
        match = OutputRegex.Match(clean);
        if (match.Success)
        {
            var startByte = int.Parse(match.Groups["byte"].Value);
            var typeStr = match.Groups["type"].Value.ToUpperInvariant();
            var bitGroup = match.Groups["bit"];

            var isBit = bitGroup.Success || typeStr == "X" || dataType == TagDataType.Bool;
            byte bitIndex = bitGroup.Success ? byte.Parse(bitGroup.Value) : (byte)0;

            return new S7Address
            {
                Area = S7AreaCode.Outputs,
                DbNumber = 0,
                StartByte = startByte,
                BitIndex = bitIndex,
                ByteLength = isBit ? 1 : byteLen,
                DataType = isBit ? TagDataType.Bool : dataType,
                RawAddress = rawAddress
            };
        }

        // 6. 尝试匹配定时器/计数器
        match = TimerCounterRegex.Match(clean);
        if (match.Success)
        {
            var kind = match.Groups["kind"].Value.ToUpperInvariant();
            var num = int.Parse(match.Groups["num"].Value);
            var isTimer = kind == "T";

            return new S7Address
            {
                Area = isTimer ? S7AreaCode.Timers : S7AreaCode.Counters,
                DbNumber = 0,
                StartByte = num,
                BitIndex = 0,
                ByteLength = 2,
                DataType = TagDataType.Int16,
                RawAddress = rawAddress
            };
        }

        throw new FormatException($"无法识别的西门子 S7 地址格式: '{rawAddress}'");
    }

    /// <summary>
    /// 获取指定数据类型的标准字节数
    /// </summary>
    public static int GetByteLength(TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Bool => 1,
            TagDataType.Int8 or TagDataType.UInt8 => 1,
            TagDataType.Int16 or TagDataType.UInt16 => 2,
            TagDataType.Int32 or TagDataType.UInt32 or TagDataType.Float => 4,
            TagDataType.Int64 or TagDataType.UInt64 or TagDataType.Double => 8,
            TagDataType.String => 256,
            TagDataType.ByteArray => 1,
            _ => 2
        };
    }
}
