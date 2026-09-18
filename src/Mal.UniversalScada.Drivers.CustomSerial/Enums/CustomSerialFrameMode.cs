namespace Mal.UniversalScada.Drivers.CustomSerial.Enums;

/// <summary>
/// 串口通信报文帧格式模式
/// </summary>
public enum CustomSerialFrameMode
{
    /// <summary>
    /// 标准二进制帧头帧尾模式 (帧头 + 地址 + 指令 + 长度 + 载荷 + 校验 + 帧尾)
    /// </summary>
    BinaryHeaderTail = 0,

    /// <summary>
    /// ASCII 文本行模式 (如传感器、电子秤、仪表按 \r\n 换行分隔的数据流)
    /// </summary>
    AsciiLine = 1,

    /// <summary>
    /// 固定长度二进制帧模式
    /// </summary>
    FixedLength = 2
}
