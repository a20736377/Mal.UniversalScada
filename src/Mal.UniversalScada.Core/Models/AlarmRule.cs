using System;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Core.Models;

/// <summary>
/// 工业报警规则定义模型。
/// 描述对指定点位的数据阈值判定准则、严重等级、迟滞死区与报警文本模板。
/// </summary>
public class AlarmRule
{
    /// <summary>
    /// 规则唯一 ID
    /// </summary>
    public string RuleId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 关联的目标点位 ID
    /// </summary>
    public string TagId { get; set; } = string.Empty;

    /// <summary>
    /// 报警规则类型 (高高限、高限、低限、低低限、变位、坏值)
    /// </summary>
    public AlarmRuleType RuleType { get; set; } = AlarmRuleType.High;

    /// <summary>
    /// 触发报警的阈值数值 (针对模拟量数值)
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// 报警严重级别
    /// </summary>
    public AlarmSeverity Severity { get; set; } = AlarmSeverity.Warning;

    /// <summary>
    /// 报警消除的迟滞回差量 (Hysteresis Deadband)。
    /// 例如：高限为 100，Deadband 为 2，则当温度升到 >=100 触发报警，必须降至 &lt;98 才会解除报警，防止在临界点频繁反复震荡。
    /// </summary>
    public double Deadband { get; set; } = 0.0;

    /// <summary>
    /// 报警描述文本模板。
    /// 支持占位符：{TagId}, {Value}, {Threshold}, {Unit}, {RuleType}
    /// </summary>
    public string MessageTemplate { get; set; } = "{TagId} 发生 {RuleType} 报警，当前值: {Value} (阈值: {Threshold})";

    /// <summary>
    /// 是否期望的布尔值触发 (仅当 RuleType == BitEqual 时有效，默认 true 即为 1 报警)
    /// </summary>
    public bool ExpectedBitValue { get; set; } = true;

    /// <summary>
    /// 是否启用该报警规则
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 格式化报警消息
    /// </summary>
    public string FormatMessage(double currentValue, string unit = "")
    {
        return MessageTemplate
            .Replace("{TagId}", TagId)
            .Replace("{Value}", currentValue.ToString("F2"))
            .Replace("{Threshold}", Threshold.ToString("F2"))
            .Replace("{Unit}", unit)
            .Replace("{RuleType}", RuleType.ToString());
    }
}
