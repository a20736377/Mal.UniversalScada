using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 工业报警评估与管理引擎接口。
/// 持续订阅实时数据总线，按规则计算越限、变位及通信异常，维护活跃报警状态机并支持人工确认与历史追溯。
/// </summary>
public interface IAlarmEngine
{
    /// <summary>
    /// 获取当前处于未恢复（活跃）状态的所有报警列表 (用于 UI 实时报警弹窗/看板展示)
    /// </summary>
    IReadOnlyList<AlarmEvent> GetActiveAlarms();

    /// <summary>
    /// 人工确认报警（操作员确认故障已知悉）
    /// </summary>
    /// <param name="eventId">报警事件唯一标识</param>
    /// <param name="operatorName">确认人姓名/工号</param>
    /// <returns>确认是否成功</returns>
    Task<bool> AcknowledgeAlarmAsync(string eventId, string operatorName);

    /// <summary>
    /// 一键确认当前所有活跃报警
    /// </summary>
    /// <param name="operatorName">确认人姓名/工号</param>
    Task AcknowledgeAllAlarmsAsync(string operatorName);

    /// <summary>
    /// 查询历史报警记录
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="tagId">可选过滤特定点位自增 ID</param>
    /// <returns>历史报警事件集合</returns>
    Task<IReadOnlyList<AlarmEvent>> QueryAlarmHistoryAsync(DateTime startTime, DateTime endTime, long? tagId = null);

    /// <summary>
    /// 当有新报警触发时触发的事件
    /// </summary>
    event EventHandler<AlarmEvent>? AlarmRaised;

    /// <summary>
    /// 当报警恢复正常（数值回到阈值内）时触发的事件
    /// </summary>
    event EventHandler<AlarmEvent>? AlarmCleared;
}
