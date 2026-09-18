using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Drivers.Siemens.Enums;

namespace Mal.UniversalScada.Drivers.Siemens.Models;

/// <summary>
/// 解析后的西门子 S7 结构化寄存器/存储区物理地址
/// </summary>
public record S7Address
{
    /// <summary>
    /// 存储区类型 (DB, Merker, Inputs, Outputs 等)
    /// </summary>
    public S7AreaCode Area { get; init; } = S7AreaCode.DataBlock;

    /// <summary>
    /// DB 块编号 (仅当 Area 为 DataBlock 时有效，其余通常为 0)
    /// </summary>
    public int DbNumber { get; init; } = 0;

    /// <summary>
    /// 起始字节偏移地址 (0-indexed)
    /// </summary>
    public int StartByte { get; init; } = 0;

    /// <summary>
    /// 位偏移 (0~7，仅当读取布尔量 Bit 时有效)
    /// </summary>
    public byte BitIndex { get; init; } = 0;

    /// <summary>
    /// 对应数据类型的字节占用长度
    /// </summary>
    public int ByteLength { get; init; } = 1;

    /// <summary>
    /// 点位对应的数据类型
    /// </summary>
    public TagDataType DataType { get; init; } = TagDataType.Int16;

    /// <summary>
    /// 是否为位操作 (Bool 类型的点位)
    /// </summary>
    public bool IsBit => DataType == TagDataType.Bool;

    /// <summary>
    /// 原始输入的地址表达式字符串
    /// </summary>
    public string RawAddress { get; init; } = string.Empty;

    /// <summary>
    /// 计算 24 位西门子 S7 寻址格式 ((StartByte * 8) + BitIndex)
    /// </summary>
    public int S7ByteBitOffset => (StartByte * 8) + (IsBit ? BitIndex : (byte)0);

    public override string ToString()
    {
        if (Area == S7AreaCode.DataBlock)
        {
            return IsBit 
                ? $"DB{DbNumber}.DBX{StartByte}.{BitIndex}" 
                : $"DB{DbNumber}.DBD{StartByte}";
        }

        string prefix = Area switch
        {
            S7AreaCode.Inputs => "I",
            S7AreaCode.Outputs => "Q",
            S7AreaCode.Merker => "M",
            S7AreaCode.Counters => "C",
            S7AreaCode.Timers => "T",
            _ => Area.ToString()
        };

        return IsBit ? $"{prefix}{StartByte}.{BitIndex}" : $"{prefix}{StartByte}";
    }
}
