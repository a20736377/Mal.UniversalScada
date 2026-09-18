namespace Mal.UniversalScada.Drivers.Siemens.Enums;

/// <summary>
/// 西门子 S7 协议存储区代号 (Area Code)
/// </summary>
public enum S7AreaCode : byte
{
    /// <summary>
    /// 系统控制区 (System Control)
    /// </summary>
    SystemControl = 0x03,

    /// <summary>
    /// 模拟量输入区 (Analog Inputs)
    /// </summary>
    AnalogInputs = 0x06,

    /// <summary>
    /// 模拟量输出区 (Analog Outputs)
    /// </summary>
    AnalogOutputs = 0x07,

    /// <summary>
    /// 计数器区 (Counters / C)
    /// </summary>
    Counters = 0x1C,

    /// <summary>
    /// 定时器区 (Timers / T)
    /// </summary>
    Timers = 0x1D,

    /// <summary>
    /// 数字量输入映像区 (Inputs / I / E)
    /// </summary>
    Inputs = 0x81,

    /// <summary>
    /// 数字量输出映像区 (Outputs / Q / A)
    /// </summary>
    Outputs = 0x82,

    /// <summary>
    /// 内部位存储器 / 标志区 (Flags / Merker / M)
    /// </summary>
    Merker = 0x83,

    /// <summary>
    /// 数据块存储区 (Data Block / DB)
    /// </summary>
    DataBlock = 0x84,

    /// <summary>
    /// 背景数据块存储区 (Instance Data Block / DI)
    /// </summary>
    InstanceDataBlock = 0x85,

    /// <summary>
    /// 局部数据区 (Local Data / L)
    /// </summary>
    LocalData = 0x86,

    /// <summary>
    /// 嵌套局部数据区 (Previous Local Data / V)
    /// </summary>
    PreviousLocalData = 0x87
}
