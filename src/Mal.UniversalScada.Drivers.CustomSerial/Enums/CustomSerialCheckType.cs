namespace Mal.UniversalScada.Drivers.CustomSerial.Enums;

/// <summary>
/// 串口私有协议校验和类型定义
/// </summary>
public enum CustomSerialCheckType
{
    /// <summary>
    /// 无校验
    /// </summary>
    None = 0,

    /// <summary>
    /// 8位累加和 (Sum of Bytes & 0xFF)
    /// </summary>
    Sum8 = 1,

    /// <summary>
    /// 8位异或校验 (BCC / XOR Check)
    /// </summary>
    Xor8 = 2,

    /// <summary>
    /// 16位 CRC Modbus (低位在先)
    /// </summary>
    Crc16Modbus = 3,

    /// <summary>
    /// 16位 CRC CCITT (高位在先)
    /// </summary>
    Crc16Ccitt = 4
}
