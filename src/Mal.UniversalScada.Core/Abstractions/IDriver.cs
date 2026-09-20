using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 工业协议驱动（Driver）统一抽象接口。
/// 所有下位机通信协议（Modbus、西门子S7、三菱MC、欧姆龙FINS、单片机私有报文）均必须实现此接口。
/// 驱动负责协议编解码、报文打包、寄存器映射换算。
/// </summary>
public interface IDriver : IDisposable
{
    /// <summary>
    /// 获取当前驱动所对应的协议类型枚举
    /// </summary>
    ProtocolType ProtocolType { get; }

    /// <summary>
    /// 获取当前驱动所支持的协议唯一名称标识 (例如: "ModbusTcp", "SiemensS7", "CustomSerialMcu")
    /// </summary>
    string ProtocolName { get; }

    /// <summary>
    /// 将驱动挂载到指定的通信通道并初始化通信上下文（如协议握手、TSAP协商、密钥验证等）
    /// </summary>
    /// <param name="channel">底层物理/网络传输通道</param>
    /// <param name="device">所服务的设备节点参数配置</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>握手/初始化是否成功</returns>
    Task<bool> InitializeAsync(IChannel channel, DeviceNode device, CancellationToken ct = default);

    /// <summary>
    /// 释放/断开当前驱动的协议会话
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 批量读取点位数据。
    /// 驱动实现内部应支持“连续地址合并打包”或在此方法中执行协议专用的多寄存器批量读取，大幅降低总线往返延时。
    /// </summary>
    /// <param name="tags">待读取的点位列表</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>以 Tag 自增 ID 为键的点位实时数据快照字典</returns>
    Task<IReadOnlyDictionary<long, TagValueSnapshot>> ReadBatchAsync(
        IEnumerable<TagNode> tags, 
        CancellationToken ct = default);

    /// <summary>
    /// 控制指令单点写入。
    /// 响应操作人员在界面上的控制、调参或单次启停指令。
    /// </summary>
    /// <param name="tag">目标点位元数据</param>
    /// <param name="value">要写入的工程值 (如 bool 启停、float 设定频率等)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>下发执行结果状态 (含往返耗时、成功标志与错误信息)</returns>
    Task<WriteResult> WriteTagAsync(
        TagNode tag, 
        object value, 
        CancellationToken ct = default);

    /// <summary>
    /// 批量写入点位数据（通常用于配方下发或多轴协同参数同步下发）
    /// </summary>
    /// <param name="writes">点位与待写入目标值的键值对集合</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>每个点位的写入执行结果集合</returns>
    Task<IReadOnlyDictionary<long, WriteResult>> WriteBatchAsync(
        IEnumerable<KeyValuePair<TagNode, object>> writes, 
        CancellationToken ct = default);
}

/// <summary>
/// 协议驱动工厂接口。
/// 用于解耦具体驱动类型与上层调度引擎，支持在运行时根据设备组态配置的 ProtocolType 动态创建或解析对应的驱动实例。
/// </summary>
public interface IDriverFactory
{
    /// <summary>
    /// 根据协议枚举类型创建或解析对应的驱动实例
    /// </summary>
    /// <param name="protocolType">协议枚举类型</param>
    /// <param name="customProtocolName">自定义协议名称标识 (仅当 protocolType 为 Custom 时需要)</param>
    /// <returns>协议驱动实例</returns>
    /// <exception cref="NotSupportedException">当未注册对应协议驱动时抛出异常</exception>
    IDriver CreateDriver(ProtocolType protocolType, string? customProtocolName = null);

    /// <summary>
    /// 根据协议名称字符串创建或解析对应的驱动实例 (兼容动态字符串解析)
    /// </summary>
    /// <param name="protocolName">协议名称 (如 "ModbusTcp", "SiemensS7")</param>
    /// <returns>协议驱动实例</returns>
    /// <exception cref="NotSupportedException">当未注册对应协议驱动时抛出异常</exception>
    IDriver CreateDriver(string protocolName);

    /// <summary>
    /// 获取当前系统中所有已注册并可用的协议驱动名称列表
    /// </summary>
    /// <returns>可用协议驱动名称集合</returns>
    IReadOnlyCollection<string> GetSupportedProtocols();
}
