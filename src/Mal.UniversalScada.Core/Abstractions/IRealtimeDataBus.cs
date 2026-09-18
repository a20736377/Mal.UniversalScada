using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 实时数据总线（Realtime Data Bus）接口。
/// 充当下位机高频采集层与上位机 UI / 业务层之间的核心解耦中枢。
/// 维护内存高频数据快照，提供高并发线程安全读写、死区判定与事件广播机制。
/// </summary>
public interface IRealtimeDataBus
{
    /// <summary>
    /// 获取指定点位的最新内存快照
    /// </summary>
    /// <param name="tagId">点位唯一 ID</param>
    /// <returns>若存在返回快照，不存在返回 null</returns>
    TagValueSnapshot? GetSnapshot(string tagId);

    /// <summary>
    /// 获取全量点位当前内存快照的只读字典 (用于 UI 页面初始装载或大屏全景轮询)
    /// </summary>
    IReadOnlyDictionary<string, TagValueSnapshot> GetAllSnapshots();

    /// <summary>
    /// 批量更新点位数据（由调度引擎采集完成后调用）。
    /// 内部执行：工程量换算、死区计算、更新内存字典，并对超过死区的点位触发广播通知。
    /// </summary>
    /// <param name="snapshots">最新采集得到的点位快照集合</param>
    void PublishSnapshots(IEnumerable<TagValueSnapshot> snapshots);

    /// <summary>
    /// 单点数据更新
    /// </summary>
    /// <param name="snapshot">最新点位快照</param>
    void PublishSnapshot(TagValueSnapshot snapshot);

    /// <summary>
    /// 订阅指定点位的数据变化事件（经死区过滤后）
    /// </summary>
    /// <param name="tagId">关心的点位 ID</param>
    /// <param name="handler">变化回调委托</param>
    /// <returns>订阅凭证 (调用 Dispose 取消订阅)</returns>
    IDisposable Subscribe(string tagId, Action<TagValueSnapshot> handler);

    /// <summary>
    /// 订阅全局所有点位的数据变化流（用于报警引擎、历史归档后台服务等）
    /// </summary>
    /// <param name="handler">变化回调委托</param>
    /// <returns>订阅凭证</returns>
    IDisposable SubscribeAll(Action<TagValueSnapshot> handler);

    /// <summary>
    /// 获取当前总线中注册纳管的点位总数量
    /// </summary>
    int RegisteredTagCount { get; }
}
