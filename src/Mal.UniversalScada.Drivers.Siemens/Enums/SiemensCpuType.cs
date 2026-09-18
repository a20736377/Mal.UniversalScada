namespace Mal.UniversalScada.Drivers.Siemens.Enums;

/// <summary>
/// 西门子 PLC CPU 硬件系列类型
/// </summary>
public enum SiemensCpuType
{
    /// <summary>
    /// S7-200 系列 (早期 PPI / CP243-1 以太网扩展)
    /// </summary>
    S7200 = 0,

    /// <summary>
    /// S7-200 SMART 系列 (自带以太网口)
    /// </summary>
    S7200Smart = 1,

    /// <summary>
    /// S7-300 系列 (默认 Rack=0, Slot=2)
    /// </summary>
    S7300 = 2,

    /// <summary>
    /// S7-400 系列 (默认 Rack=0, Slot=3 或 2)
    /// </summary>
    S7400 = 3,

    /// <summary>
    /// S7-1200 系列 (默认 Rack=0, Slot=1)
    /// </summary>
    S71200 = 4,

    /// <summary>
    /// S7-1500 系列 (默认 Rack=0, Slot=1)
    /// </summary>
    S71500 = 5
}
