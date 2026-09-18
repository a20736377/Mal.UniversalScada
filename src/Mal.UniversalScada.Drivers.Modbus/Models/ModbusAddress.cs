using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Drivers.Modbus.Enums;

namespace Mal.UniversalScada.Drivers.Modbus.Models;

/// <summary>
/// 解析后的 Modbus 物理寄存器地址结构
/// </summary>
public record ModbusAddress
{
    /// <summary>
    /// 目标寄存器类型 (线圈、离散输入、输入寄存器、保持寄存器)
    /// </summary>
    public ModbusRegisterType RegisterType { get; init; } = ModbusRegisterType.HoldingRegister;

    /// <summary>
    /// 协议报文线路上实际传输的起始地址 (0-indexed，范围 0 ~ 65535)
    /// </summary>
    public ushort StartAddress { get; init; }

    /// <summary>
    /// 本点位占用/需读取的寄存器数量 (16位寄存器个数或线圈位数量)
    /// </summary>
    public ushort RegisterCount { get; init; } = 1;

    /// <summary>
    /// 寄存器内部位索引 (0 ~ 15)。若针对寄存器整字或原生线圈位，此项为 null
    /// 例如 40001.5 寻址保持寄存器 0 的第 5 位，则 StartAddress=0, BitIndex=5
    /// </summary>
    public int? BitIndex { get; init; }

    /// <summary>
    /// 对应的工程数据类型
    /// </summary>
    public TagDataType DataType { get; init; } = TagDataType.Int16;

    /// <summary>
    /// 原始组态地址字符串 (如 "40001.0", "0x0001", "30005")
    /// </summary>
    public string RawAddress { get; init; } = string.Empty;

    /// <summary>
    /// 是否为寄存器内的按位寻址
    /// </summary>
    public bool IsBitInRegister => BitIndex.HasValue;

    /// <summary>
    /// 是否为原生位数据区 (线圈或离散输入)
    /// </summary>
    public bool IsNativeBitArea => RegisterType is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput;
}
