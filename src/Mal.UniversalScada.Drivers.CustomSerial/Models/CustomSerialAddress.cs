using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Drivers.CustomSerial.Models;

/// <summary>
/// 解析后的串口自定义点位地址模型
/// </summary>
public record CustomSerialAddress
{
    /// <summary>
    /// 功能码 / 指令字 (例如 0x01, 0x03, 0x05)
    /// </summary>
    public byte Command { get; init; } = 0x01;

    /// <summary>
    /// 数据在有效负载 (Payload) 中的字节起始偏移 (0-based)
    /// </summary>
    public int ByteOffset { get; init; }

    /// <summary>
    /// 字节内部位索引 (0 ~ 7)。若为整型或浮点等非位点位，此项为 null
    /// </summary>
    public int? BitIndex { get; init; }

    /// <summary>
    /// ASCII 文本分隔模式下的字段索引 (例如 "DATA,25.4,60.2" 中索引 1 为温度，索引 2 为湿度)
    /// </summary>
    public int? FieldIndex { get; init; }

    /// <summary>
    /// 数据类型
    /// </summary>
    public TagDataType DataType { get; init; } = TagDataType.Int16;

    /// <summary>
    /// 原始地址字符串
    /// </summary>
    public string RawAddress { get; init; } = string.Empty;

    /// <summary>
    /// 是否为字节内按位寻址
    /// </summary>
    public bool IsBit => BitIndex.HasValue;
}
