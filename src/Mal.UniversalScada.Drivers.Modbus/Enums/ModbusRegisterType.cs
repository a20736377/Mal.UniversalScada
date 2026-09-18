namespace Mal.UniversalScada.Drivers.Modbus.Enums;

/// <summary>
/// Modbus 标准四种数据区寄存器类型定义
/// </summary>
public enum ModbusRegisterType
{
    /// <summary>
    /// 线圈状态 (0x / 00001~09999 / 000001~065536)
    /// 读功能码: FC01, 单写: FC05, 多写: FC15 (可读写布尔量)
    /// </summary>
    Coil = 1,

    /// <summary>
    /// 离散输入状态 (1x / 10001~19999 / 100001~165536)
    /// 读功能码: FC02 (只读布尔量)
    /// </summary>
    DiscreteInput = 2,

    /// <summary>
    /// 输入寄存器 (3x / 30001~39999 / 300001~365536)
    /// 读功能码: FC04 (只读 16位 寄存器)
    /// </summary>
    InputRegister = 3,

    /// <summary>
    /// 保持寄存器 (4x / 40001~49999 / 400001~465536)
    /// 读功能码: FC03, 单写: FC06, 多写: FC16 (可读写 16位 寄存器)
    /// </summary>
    HoldingRegister = 4
}
