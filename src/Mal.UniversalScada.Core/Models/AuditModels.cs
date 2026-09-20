using System;

namespace Mal.UniversalScada.Core.Models;

/// <summary>
/// 工业合规操作安全审计日志实体 (Audit Record)。
/// 严密记录所有关键操作动作，不可篡改，符合 GMP / FDA 21 CFR Part 11 与工业现场审计追溯标准。
/// </summary>
public class AuditRecord
{
    /// <summary>
    /// 审计日志全局唯一 ID
    /// </summary>
    public string RecordId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 动作发生精确时间
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// 执行操作人员工号 / 登录名
    /// </summary>
    public string OperatorName { get; set; } = "Operator";

    /// <summary>
    /// 操作动作类型 (如: "TagWrite", "RecipeApply", "AlarmAck", "ViewSwitch", "ParamTune")
    /// </summary>
    public string ActionType { get; set; } = "TagWrite";

    /// <summary>
    /// 关联目标标识 (点位 ID / 设备 ID / 配方 ID 等)
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// 变更前值 (若有)
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// 变更后目标值 (若有)
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// 下发/执行结果是否成功
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// 错误/失败详情信息 (若有)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 执行耗时 (毫秒)
    /// </summary>
    public long ElapsedMs { get; set; }

    /// <summary>
    /// 操作来源客户端 IP / 主机名
    /// </summary>
    public string ClientInfo { get; set; } = Environment.MachineName;
}
