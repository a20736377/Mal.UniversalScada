namespace Mal.UniversalScada.Drivers.Modbus.Enums;

/// <summary>
/// 工业 Modbus 多字节与字交换模式 (Endianness & Word Swap)
/// </summary>
public enum ModbusEndian
{
    /// <summary>
    /// 大端序 (ABCD / Big-Endian / Motorola)
    /// 高字节在前，高字在前。Modbus 标准默认模式。
    /// 32位数据排布: Byte 0 (A), Byte 1 (B), Byte 2 (C), Byte 3 (D)
    /// </summary>
    ABCD = 0,

    /// <summary>
    /// 字交换 (CDAB / Little-Endian Word Swap)
    /// 高字节在前，低字在前。三菱、欧姆龙、部分西门子/第三方仪表及组态软件常用。
    /// 32位数据排布: Byte 2 (C), Byte 3 (D), Byte 0 (A), Byte 1 (B)
    /// </summary>
    CDAB = 1,

    /// <summary>
    /// 字节交换 (BADC / Big-Endian Byte Swap)
    /// 低字节在前，高字在前。
    /// 32位数据排布: Byte 1 (B), Byte 0 (A), Byte 3 (D), Byte 2 (C)
    /// </summary>
    BADC = 2,

    /// <summary>
    /// 小端序 (DCBA / Little-Endian / Intel)
    /// 低字节在前，低字在前。
    /// 32位数据排布: Byte 3 (D), Byte 2 (C), Byte 1 (B), Byte 0 (A)
    /// </summary>
    DCBA = 3
}
