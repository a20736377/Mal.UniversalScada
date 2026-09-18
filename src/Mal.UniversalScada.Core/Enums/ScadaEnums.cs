namespace Mal.UniversalScada.Core.Enums;

/// <summary>
/// 表示通信通道（链路）的连接状态
/// </summary>
public enum ChannelState
{
    /// <summary>
    /// 通道处于断开关闭状态
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// 正在尝试建立连接/打开链路
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// 通道正常连接并处于激活就绪状态
    /// </summary>
    Connected = 2,

    /// <summary>
    /// 通道通信发生异常或硬件故障
    /// </summary>
    Faulted = 3
}

/// <summary>
/// 通信通道（链路）的传输介质类型
/// </summary>
public enum ChannelType
{
    /// <summary>
    /// 串行通信口 (RS232 / RS485 / 虚拟串口)
    /// </summary>
    SerialPort = 0,

    /// <summary>
    /// TCP 客户端 (以太网连接下位机或网关)
    /// </summary>
    TcpClient = 1,

    /// <summary>
    /// TCP 服务端 (上位机作为监听服务端)
    /// </summary>
    TcpServer = 2,

    /// <summary>
    /// UDP 用户数据报通信
    /// </summary>
    Udp = 3
}

/// <summary>
/// 表示点位（Tag）采样数据的通信品质戳（Quality Code）
/// 遵循工业 OPC / SCADA 规范，用于指示当前实时值的置信度
/// </summary>
public enum QualityCode
{
    /// <summary>
    /// 良好 (数据采集正常，数值高度可信)
    /// </summary>
    Good = 0,

    /// <summary>
    /// 不确定 (初始化未采集、等待首帧或处于过渡态)
    /// </summary>
    Uncertain = 1,

    /// <summary>
    /// 坏值 (数据不可用，值无效)
    /// </summary>
    Bad = 2,

    /// <summary>
    /// 通信故障 (下位机未应答、物理断线、CRC/校验失败)
    /// </summary>
    CommFailure = 3,

    /// <summary>
    /// 设备离线或被禁用
    /// </summary>
    DeviceOffline = 4,

    /// <summary>
    /// 数据溢出或超出工程量程极限
    /// </summary>
    OutOfRange = 5
}

/// <summary>
/// 表示点位所映射的下位机数据类型
/// </summary>
public enum TagDataType
{
    /// <summary>
    /// 布尔量 (1 Bit，如开闭状态、故障指示、线圈)
    /// </summary>
    Bool = 0,

    /// <summary>
    /// 8 位有符号字节 (SByte)
    /// </summary>
    Int8 = 1,

    /// <summary>
    /// 8 位无符号字节 (Byte)
    /// </summary>
    UInt8 = 2,

    /// <summary>
    /// 16 位有符号整型 (Short / 对应 PLC INT)
    /// </summary>
    Int16 = 3,

    /// <summary>
    /// 16 位无符号整型 (UShort / 对应 PLC UINT / WORD)
    /// </summary>
    UInt16 = 4,

    /// <summary>
    /// 32 位有符号整型 (Int / 对应 PLC DINT)
    /// </summary>
    Int32 = 5,

    /// <summary>
    /// 32 位无符号整型 (UInt / 对应 PLC UDINT / DWORD)
    /// </summary>
    UInt32 = 6,

    /// <summary>
    /// 64 位有符号整型 (Long / 对应 PLC LINT)
    /// </summary>
    Int64 = 7,

    /// <summary>
    /// 64 位无符号整型 (ULong / 对应 PLC ULINT)
    /// </summary>
    UInt64 = 8,

    /// <summary>
    /// 32 位单精度浮点数 (Float / 对应 PLC REAL)
    /// </summary>
    Float = 9,

    /// <summary>
    /// 64 位双精度浮点数 (Double / 对应 PLC LREAL)
    /// </summary>
    Double = 10,

    /// <summary>
    /// 字符串类型 (ASCII / UTF-8)
    /// </summary>
    String = 11,

    /// <summary>
    /// 原始字节数组 (用于下发私有协议报文或大块数据)
    /// </summary>
    ByteArray = 12
}

/// <summary>
/// 点位访问权限定义
/// </summary>
public enum TagAccessMode
{
    /// <summary>
    /// 只读 (如传感器模拟量、运行状态、只读计数器)
    /// </summary>
    ReadOnly = 0,

    /// <summary>
    /// 只写 (如下发单次触发指令、复位脉冲)
    /// </summary>
    WriteOnly = 1,

    /// <summary>
    /// 可读可写 (如参数设定值、目标速度、启停控制寄存器)
    /// </summary>
    ReadWrite = 2
}

/// <summary>
/// 报警严重性级别
/// </summary>
public enum AlarmSeverity
{
    /// <summary>
    /// 提示信息 (Info)
    /// </summary>
    Information = 0,

    /// <summary>
    /// 低级警告 (Warning) - 如接近上下限阈值
    /// </summary>
    Warning = 1,

    /// <summary>
    /// 一般故障 (Error) - 需要操作员关注处理
    /// </summary>
    Error = 2,

    /// <summary>
    /// 紧急/严重危险报警 (Critical) - 触发联锁停机或严重工艺偏差
    /// </summary>
    Critical = 3
}

/// <summary>
/// 下位机通信协议类型枚举
/// </summary>
public enum ProtocolType
{
    /// <summary>
    /// Modbus TCP 协议 (基于以太网)
    /// </summary>
    ModbusTcp = 1,

    /// <summary>
    /// Modbus RTU 协议 (基于串口 RS485/RS232)
    /// </summary>
    ModbusRtu = 2,

    /// <summary>
    /// Modbus ASCII 协议 (基于串口)
    /// </summary>
    ModbusAscii = 3,

    /// <summary>
    /// 西门子 S7 协议 (支持 S7-200/Smart/300/400/1200/1500)
    /// </summary>
    SiemensS7 = 10,

    /// <summary>
    /// 三菱 MC 协议 (MELSEC Communication，支持 Q/L/iQ-R/FX 系列)
    /// </summary>
    MitsubishiMc = 20,

    /// <summary>
    /// 欧姆龙 FINS 协议 (FINS UDP/TCP)
    /// </summary>
    OmronFins = 30,

    /// <summary>
    /// 单片机/嵌入式私有串口协议 (自定义帧头帧尾与校验)
    /// </summary>
    CustomSerial = 40,

    /// <summary>
    /// 物联网 MQTT 遥测协议 (JSON / 二进制 Payload)
    /// </summary>
    Mqtt = 50,

    /// <summary>
    /// OPC 统一架构 (OPC UA Client)
    /// </summary>
    OpcUa = 60,

    /// <summary>
    /// 自定义 / 其它私有扩展协议
    /// </summary>
    Custom = 99
}
