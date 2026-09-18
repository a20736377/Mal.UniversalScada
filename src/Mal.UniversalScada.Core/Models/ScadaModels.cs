using System.Text.Json.Serialization;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Core.Models;

/// <summary>
/// 点位（Tag）定义元数据模型，描述下位机变量的地址映射与工程转换属性
/// </summary>
public class TagNode
{
    /// <summary>
    /// 点位唯一标识符 (全局唯一，建议格式如: Device1.Motor_Speed)
    /// </summary>
    public string TagId { get; set; } = string.Empty;

    /// <summary>
    /// 所属逻辑设备 ID
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// 点位友好名称 / 中文描述 (如: 1号主电机当前转速)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 下位机硬件寄存器物理地址 (如 Modbus 的 "40001"、西门子的 "DB1.DBD0"、三菱的 "D100")
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// 数据类型 (PLC / 单片机变量类型)
    /// </summary>
    public TagDataType DataType { get; set; } = TagDataType.Int16;

    /// <summary>
    /// 访问模式 (只读 / 只写 / 读写)
    /// </summary>
    public TagAccessMode AccessMode { get; set; } = TagAccessMode.ReadOnly;

    /// <summary>
    /// 线性换算比例系数 k (工程量换算: 实际工程值 y = k * 原始采集值 + b)
    /// </summary>
    public double ScaleFactor { get; set; } = 1.0;

    /// <summary>
    /// 线性换算偏移量 b
    /// </summary>
    public double Offset { get; set; } = 0.0;

    /// <summary>
    /// 工程单位 (如: "℃", "rpm", "bar", "MPa", "m/s")
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// 扫描采集周期 (毫秒)。如果为 0 则遵循设备默认扫描周期
    /// </summary>
    public int ScanIntervalMs { get; set; } = 100;

    /// <summary>
    /// 变化死区绝对值 (Deadband)。当 |新值 - 旧值| > Deadband 时才触发事件与存储
    /// </summary>
    public double Deadband { get; set; } = 0.0;

    /// <summary>
    /// 是否需要归档记录到历史数据库
    /// </summary>
    public bool IsHistorical { get; set; } = true;
}

/// <summary>
/// 实时点位数据快照 (不可变值对象，用于高并发安全传递)
/// </summary>
public record TagValueSnapshot
{
    /// <summary>
    /// 对应的点位唯一 ID
    /// </summary>
    public string TagId { get; init; } = string.Empty;

    /// <summary>
    /// 换算后的工程量当前值 (可为 bool, short, float, double, string 等)
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// 下位机读取到的原始未经换算的值 (用于调试与底层报文验证)
    /// </summary>
    public object? RawValue { get; init; }

    /// <summary>
    /// 本次采样的品质戳 (Good, Bad, CommFailure 等)
    /// </summary>
    public QualityCode Quality { get; init; } = QualityCode.Uncertain;

    /// <summary>
    /// 数据采样的精确时间戳 (UTC 或本地时间)
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>
    /// 通用数值转换辅助方法 (转换为浮点数以供计算与图表绘制)
    /// </summary>
    public double ToDouble(double defaultValue = 0.0)
    {
        if (Value is null) return defaultValue;
        return Convert.ToDouble(Value);
    }
}

/// <summary>
/// 设备节点模型，表示挂载在通信链路上的具体逻辑设备
/// </summary>
public class DeviceNode
{
    /// <summary>
    /// 设备唯一标识 ID (如: "PLC_Main", "MCU_Sensor_01")
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// 设备名称 (如: "主线灌装PLC")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 所绑定的通信通道 ID (对应一个 Channel)
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// 使用的通信协议类型枚举
    /// </summary>
    public ProtocolType ProtocolType { get; set; } = ProtocolType.ModbusTcp;

    /// <summary>
    /// 自定义协议扩展标识名称 (仅当 ProtocolType 为 Custom 时使用，用于区分不同的第三方私有协议驱动)
    /// </summary>
    public string? CustomProtocolName { get; set; }

    /// <summary>
    /// 从站站号 / 单元标识符 (UnitId / SlaveId / Rack / Slot 等)
    /// </summary>
    public int StationAddress { get; set; } = 1;

    /// <summary>
    /// 默认周期轮询采集间隔 (毫秒)
    /// </summary>
    public int DefaultPollIntervalMs { get; set; } = 100;

    /// <summary>
    /// 连续通信超时判定阈值 (毫秒)
    /// </summary>
    public int TimeoutMs { get; set; } = 2000;

    /// <summary>
    /// 是否启用该设备采集
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 控制指令下发写入结果
/// </summary>
public record WriteResult
{
    /// <summary>
    /// 写入是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 目标点位 ID
    /// </summary>
    public string TagId { get; init; } = string.Empty;

    /// <summary>
    /// 本次下发写入的值
    /// </summary>
    public object? TargetValue { get; init; }

    /// <summary>
    /// 错误/失败详情信息 (若成功则为空)
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 下发与下位机应答往返耗时 (毫秒)
    /// </summary>
    public long RoundTripTimeMs { get; init; }

    /// <summary>
    /// 便捷构造成功结果
    /// </summary>
    public static WriteResult Success(string tagId, object? val, long elapsedMs = 0) =>
        new() { IsSuccess = true, TagId = tagId, TargetValue = val, RoundTripTimeMs = elapsedMs };

    /// <summary>
    /// 便捷构造失败结果
    /// </summary>
    public static WriteResult Failed(string tagId, object? val, string error, long elapsedMs = 0) =>
        new() { IsSuccess = false, TagId = tagId, TargetValue = val, ErrorMessage = error, RoundTripTimeMs = elapsedMs };
}

/// <summary>
/// 历史数据持久化归档记录
/// </summary>
public record TagHistoryRecord
{
    /// <summary>
    /// 点位全局 ID
    /// </summary>
    public string TagId { get; init; } = string.Empty;

    /// <summary>
    /// 采样时间戳
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// 浮点工程值
    /// </summary>
    public double Value { get; init; }

    /// <summary>
    /// 品质戳
    /// </summary>
    public QualityCode Quality { get; init; }
}

/// <summary>
/// 针对图表趋势渲染优化的轻量级时序数据点 (用于 LTTB 降采样输出)
/// </summary>
public record TimeSeriesPoint(DateTime Time, double Value);

/// <summary>
/// 工业报警事件实体
/// </summary>
public class AlarmEvent
{
    /// <summary>
    /// 报警事件唯一标识
    /// </summary>
    public string EventId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 触发报警的点位 ID
    /// </summary>
    public string TagId { get; set; } = string.Empty;

    /// <summary>
    /// 报警消息描述 (如: "1号电机温度过高 (98.5℃)")
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 报警等级
    /// </summary>
    public AlarmSeverity Severity { get; set; } = AlarmSeverity.Warning;

    /// <summary>
    /// 触发时刻的值
    /// </summary>
    public double TriggerValue { get; set; }

    /// <summary>
    /// 设定的报警阈值
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// 报警首次触发时间
    /// </summary>
    public DateTime TriggerTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 报警恢复正常时间 (若未恢复则为 null)
    /// </summary>
    public DateTime? ClearedTime { get; set; }

    /// <summary>
    /// 是否已被操作员人工确认
    /// </summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>
    /// 确认操作员工号/姓名
    /// </summary>
    public string? AcknowledgedBy { get; set; }

    /// <summary>
    /// 确认时间
    /// </summary>
    public DateTime? AcknowledgedTime { get; set; }
}

/// <summary>
/// 通道配置模型，定义与下位机建立物理或网络通信链路的连接参数
/// </summary>
public class ChannelConfig
{
    /// <summary>
    /// 通道唯一标识 ID (如 "CH_COM1", "CH_TCP_LINE1")
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// 通道友好名称 (如 "1号产线串口总线", "灌装机以太网链路")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 通道传输类型 (串口 / TCP客户端 / UDP等)
    /// </summary>
    public ChannelType ChannelType { get; set; } = ChannelType.TcpClient;

    /// <summary>
    /// 串口号 (如 "COM1", "COM3") - 仅当 ChannelType 为 SerialPort 时有效
    /// </summary>
    public string PortName { get; set; } = "COM1";

    /// <summary>
    /// 串口波特率 (如 9600, 19200, 115200)
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// 数据位 (通常为 8 或 7)
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// 停止位 ("One", "Two", "OnePointFive")
    /// </summary>
    public string StopBits { get; set; } = "One";

    /// <summary>
    /// 校验位 ("None", "Even", "Odd", "Mark", "Space")
    /// </summary>
    public string Parity { get; set; } = "None";

    /// <summary>
    /// 远程主机 IP 地址或主机名 - 仅当 ChannelType 为 TcpClient/Udp 时有效
    /// </summary>
    public string Host { get; set; } = "192.168.1.100";

    /// <summary>
    /// 远程网络端口号 (如 Modbus 默认 502, S7 默认 102)
    /// </summary>
    public int Port { get; set; } = 502;

    /// <summary>
    /// 读取通信超时时间 (毫秒)
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 1500;

    /// <summary>
    /// 写入通信超时时间 (毫秒)
    /// </summary>
    public int WriteTimeoutMs { get; set; } = 1500;

    /// <summary>
    /// 断线自动重连间隔时间 (毫秒)
    /// </summary>
    public int ReconnectIntervalMs { get; set; } = 5000;

    /// <summary>
    /// 是否启用该通道
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
