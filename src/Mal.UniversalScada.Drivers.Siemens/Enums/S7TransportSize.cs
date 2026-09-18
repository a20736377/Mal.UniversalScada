namespace Mal.UniversalScada.Drivers.Siemens.Enums;

/// <summary>
/// 西门子 S7 协议请求项与应答项传输数据类型/尺寸规范
/// </summary>
public enum S7TransportSize : byte
{
    /// <summary>
    /// 未指定 / 空
    /// </summary>
    Null = 0x00,

    /// <summary>
    /// 位数据 (Bit) - 请求时取值
    /// </summary>
    Bit = 0x01,

    /// <summary>
    /// 字节数据 (Byte) - 请求时取值
    /// </summary>
    Byte = 0x02,

    /// <summary>
    /// 字符数据 (Char)
    /// </summary>
    Char = 0x03,

    /// <summary>
    /// 字数据 (Word)
    /// </summary>
    Word = 0x04,

    /// <summary>
    /// 16 位整型 (Int)
    /// </summary>
    Int = 0x05,

    /// <summary>
    /// 双字数据 (DWord)
    /// </summary>
    DWord = 0x06,

    /// <summary>
    /// 32 位整型 (DInt)
    /// </summary>
    DInt = 0x07,

    /// <summary>
    /// 32 位单精度浮点 (Real)
    /// </summary>
    Real = 0x08,

    /// <summary>
    /// 八位字节流 (Octet String)
    /// </summary>
    OctetString = 0x09
}

/// <summary>
/// S7 应答数据中的 TransportSize 代码
/// </summary>
public enum S7ResponseTransportSize : byte
{
    Null = 0x00,
    Bit = 0x03,
    ByteWordDWord = 0x04,
    Real = 0x07,
    OctetString = 0x09
}

/// <summary>
/// 西门子 S7 读写返回码
/// </summary>
public enum S7ReturnCode : byte
{
    /// <summary>
    /// 成功读取/写入
    /// </summary>
    Success = 0xFF,

    /// <summary>
    /// 硬件错误
    /// </summary>
    HardwareFault = 0x01,

    /// <summary>
    /// 对象不存在 / 无效地址
    /// </summary>
    ObjectDoesNotExist = 0x03,

    /// <summary>
    /// 地址超出物理/组态量程范围
    /// </summary>
    AddressOutOfRange = 0x05,

    /// <summary>
    /// 数据类型不支持 / 无效请求
    /// </summary>
    DataTypeUnsupported = 0x06,

    /// <summary>
    /// 数据类型不匹配
    /// </summary>
    DataTypeInconsistent = 0x07,

    /// <summary>
    /// 对象访问权限不足或被写保护
    /// </summary>
    AccessDenied = 0x0A
}
