using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 读写双优先级调度引擎（Priority Scheduler）接口。
/// 协调所有通道与下位机的周期性轮询采集，并保障用户操作控制下发（写指令）能够享有绝对最高优先级即时插队执行。
/// </summary>
public interface IPriorityScheduler : IAsyncDisposable
{
    /// <summary>
    /// 获取当前调度引擎是否正在运行
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 启动调度引擎（开始根据拓扑配置进行多设备并发轮询采集）
    /// </summary>
    /// <param name="ct">取消令牌</param>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// 停止调度引擎并优雅排空正在执行的通信会话
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 提交高优先级控制写操作（供 UI 操作员点击按钮、下发设定值时调用）。
    /// 该方法将指令直接插入目标设备通道的高优队列头部，暂停下一批次读取，优先下发写报文并等待下位机响应。
    /// </summary>
    /// <param name="tagId">目标点位 ID</param>
    /// <param name="value">目标写入值</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>写入结果（成功/失败状态、错误描述、耗时）</returns>
    Task<WriteResult> EnqueueWriteAsync(string tagId, object value, CancellationToken ct = default);

    /// <summary>
    /// 批量提交控制写操作（如执行配方参数同步下发）
    /// </summary>
    /// <param name="writes">点位 ID 与待写入值的键值集合</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>各点位的写入结果字典</returns>
    Task<IReadOnlyDictionary<string, WriteResult>> EnqueueBatchWriteAsync(
        IEnumerable<KeyValuePair<string, object>> writes, 
        CancellationToken ct = default);

    /// <summary>
    /// 触发立即执行一次指定设备的指定点位强制刷新采集（跳过常规轮询等待）
    /// </summary>
    /// <param name="deviceId">目标设备 ID</param>
    Task TriggerImmediatePollAsync(string deviceId);
}
