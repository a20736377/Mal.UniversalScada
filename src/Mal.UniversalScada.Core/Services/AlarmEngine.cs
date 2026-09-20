using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Services;

/// <summary>
/// 工业报警评估与管理引擎（Alarm Engine）核心实现。
/// 持续订阅实时数据总线，执行高高限/高限/低限/低低限/变位及通信坏值多级规则判定，
/// 维护未恢复/未确认报警状态机，提供人工确认与历史追溯机制。
/// </summary>
public class AlarmEngine : IAlarmEngine, IDisposable
{
    private readonly IRealtimeDataBus _dataBus;
    private readonly IDisposable _busSubscription;

    // 规则索引 (Key: TagId)
    private readonly ConcurrentDictionary<string, List<AlarmRule>> _rulesByTag = new(StringComparer.OrdinalIgnoreCase);

    // 当前处于活跃状态的报警 (Key: RuleId)
    private readonly ConcurrentDictionary<string, AlarmEvent> _activeAlarms = new(StringComparer.OrdinalIgnoreCase);

    // 历史报警内存环形队列 / 列表
    private readonly List<AlarmEvent> _alarmHistory = new();
    private readonly object _historyLock = new();
    private const int MaxHistoryRetention = 10000;

    public event EventHandler<AlarmEvent>? AlarmRaised;
    public event EventHandler<AlarmEvent>? AlarmCleared;

    public AlarmEngine(IRealtimeDataBus dataBus)
    {
        _dataBus = dataBus ?? throw new ArgumentNullException(nameof(dataBus));
        _busSubscription = _dataBus.SubscribeAll(OnSnapshotReceived);
    }

    /// <summary>
    /// 注册报警规则
    /// </summary>
    public void RegisterRule(AlarmRule rule)
    {
        if (rule == null || string.IsNullOrWhiteSpace(rule.TagId)) return;
        var list = _rulesByTag.GetOrAdd(rule.TagId, _ => new List<AlarmRule>());
        lock (list)
        {
            list.RemoveAll(r => r.RuleId == rule.RuleId);
            list.Add(rule);
        }
    }

    /// <summary>
    /// 批量注册报警规则
    /// </summary>
    public void RegisterRules(IEnumerable<AlarmRule> rules)
    {
        if (rules == null) return;
        foreach (var rule in rules)
        {
            RegisterRule(rule);
        }
    }

    /// <summary>
    /// 清空所有规则
    /// </summary>
    public void ClearRules()
    {
        _rulesByTag.Clear();
    }

    /// <inheritdoc />
    public IReadOnlyList<AlarmEvent> GetActiveAlarms()
    {
        return _activeAlarms.Values.OrderByDescending(a => a.TriggerTime).ToList();
    }

    /// <inheritdoc />
    public Task<bool> AcknowledgeAlarmAsync(string eventId, string operatorName)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return Task.FromResult(false);

        // 查找活跃报警
        var active = _activeAlarms.Values.FirstOrDefault(a => string.Equals(a.EventId, eventId, StringComparison.OrdinalIgnoreCase));
        if (active != null)
        {
            active.IsAcknowledged = true;
            active.AcknowledgedBy = operatorName;
            active.AcknowledgedTime = DateTime.Now;

            // 如果该报警此前已经消除（ClearedTime != null），确认后即可移出活跃列表
            if (active.ClearedTime.HasValue)
            {
                var ruleKey = _activeAlarms.FirstOrDefault(kvp => kvp.Value == active).Key;
                if (ruleKey != null)
                {
                    _activeAlarms.TryRemove(ruleKey, out _);
                }
            }

            return Task.FromResult(true);
        }

        // 查找历史报警标记确认
        lock (_historyLock)
        {
            var hist = _alarmHistory.FirstOrDefault(a => string.Equals(a.EventId, eventId, StringComparison.OrdinalIgnoreCase));
            if (hist != null)
            {
                hist.IsAcknowledged = true;
                hist.AcknowledgedBy = operatorName;
                hist.AcknowledgedTime = DateTime.Now;
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task AcknowledgeAllAlarmsAsync(string operatorName)
    {
        var now = DateTime.Now;
        foreach (var kvp in _activeAlarms)
        {
            var alarm = kvp.Value;
            alarm.IsAcknowledged = true;
            alarm.AcknowledgedBy = operatorName;
            alarm.AcknowledgedTime = now;

            // 如果此前已恢复，确认后移出活跃区
            if (alarm.ClearedTime.HasValue)
            {
                _activeAlarms.TryRemove(kvp.Key, out _);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AlarmEvent>> QueryAlarmHistoryAsync(DateTime startTime, DateTime endTime, string? tagId = null)
    {
        lock (_historyLock)
        {
            var query = _alarmHistory.Where(a => a.TriggerTime >= startTime && a.TriggerTime <= endTime);
            if (!string.IsNullOrWhiteSpace(tagId))
            {
                query = query.Where(a => string.Equals(a.TagId, tagId, StringComparison.OrdinalIgnoreCase));
            }
            return Task.FromResult<IReadOnlyList<AlarmEvent>>(query.OrderByDescending(a => a.TriggerTime).ToList());
        }
    }

    /// <summary>
    /// 接收数据总线快照并执行规则评估
    /// </summary>
    private void OnSnapshotReceived(TagValueSnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.TagId)) return;
        if (!_rulesByTag.TryGetValue(snapshot.TagId, out var rules) || rules.Count == 0) return;

        List<AlarmRule> ruleCopies;
        lock (rules)
        {
            ruleCopies = rules.Where(r => r.IsEnabled).ToList();
        }

        foreach (var rule in ruleCopies)
        {
            EvaluateRule(rule, snapshot);
        }
    }

    /// <summary>
    /// 评估单条规则对当前快照的触发或解除状态
    /// </summary>
    private void EvaluateRule(AlarmRule rule, TagValueSnapshot snapshot)
    {
        bool isConditionMet = false;
        double currentVal = 0.0;
        bool isNumeric = false;

        if (snapshot.Value != null)
        {
            try
            {
                currentVal = Convert.ToDouble(snapshot.Value);
                isNumeric = true;
            }
            catch { }
        }

        // 1. 判断是否满足报警触发条件
        switch (rule.RuleType)
        {
            case AlarmRuleType.BadQuality:
                isConditionMet = snapshot.Quality != QualityCode.Good;
                break;

            case AlarmRuleType.BitEqual:
                if (snapshot.Value is bool b)
                {
                    isConditionMet = b == rule.ExpectedBitValue;
                }
                else if (isNumeric)
                {
                    isConditionMet = (currentVal != 0) == rule.ExpectedBitValue;
                }
                break;

            case AlarmRuleType.HighHigh:
            case AlarmRuleType.High:
                if (isNumeric)
                {
                    isConditionMet = currentVal >= rule.Threshold;
                }
                break;

            case AlarmRuleType.Low:
            case AlarmRuleType.LowLow:
                if (isNumeric)
                {
                    isConditionMet = currentVal <= rule.Threshold;
                }
                break;
        }

        bool isCurrentlyActive = _activeAlarms.TryGetValue(rule.RuleId, out var existingAlarm);

        if (isConditionMet)
        {
            // 满足触发条件
            if (!isCurrentlyActive)
            {
                // 新产生报警
                var newAlarm = new AlarmEvent
                {
                    EventId = Guid.NewGuid().ToString("N"),
                    TagId = snapshot.TagId,
                    Message = rule.FormatMessage(currentVal),
                    Severity = rule.Severity,
                    TriggerValue = currentVal,
                    Threshold = rule.Threshold,
                    TriggerTime = snapshot.Timestamp,
                    ClearedTime = null,
                    IsAcknowledged = false
                };

                _activeAlarms[rule.RuleId] = newAlarm;

                lock (_historyLock)
                {
                    _alarmHistory.Add(newAlarm);
                    if (_alarmHistory.Count > MaxHistoryRetention)
                    {
                        _alarmHistory.RemoveRange(0, 1000);
                    }
                }

                // 触发事件广播
                try { AlarmRaised?.Invoke(this, newAlarm); } catch { }
            }
            else if (existingAlarm != null && existingAlarm.ClearedTime.HasValue)
            {
                // 此前消除但未确认，再次触发重置消除时间
                existingAlarm.ClearedTime = null;
                existingAlarm.TriggerValue = currentVal;
            }
        }
        else
        {
            // 不满足触发条件（即恢复正常，需结合迟滞回差判定）
            if (isCurrentlyActive && existingAlarm != null && !existingAlarm.ClearedTime.HasValue)
            {
                bool isTrulyCleared = true;

                // 迟滞回差判定 (Hysteresis)
                if (rule.Deadband > 0 && isNumeric)
                {
                    if (rule.RuleType == AlarmRuleType.High || rule.RuleType == AlarmRuleType.HighHigh)
                    {
                        // 必须降至 Threshold - Deadband 以下才视为恢复
                        isTrulyCleared = currentVal < (rule.Threshold - rule.Deadband);
                    }
                    else if (rule.RuleType == AlarmRuleType.Low || rule.RuleType == AlarmRuleType.LowLow)
                    {
                        // 必须升至 Threshold + Deadband 以上才视为恢复
                        isTrulyCleared = currentVal > (rule.Threshold + rule.Deadband);
                    }
                }

                if (isTrulyCleared)
                {
                    existingAlarm.ClearedTime = snapshot.Timestamp;

                    // 若已被操作员确认，直接移出活跃区
                    if (existingAlarm.IsAcknowledged)
                    {
                        _activeAlarms.TryRemove(rule.RuleId, out _);
                    }

                    // 触发解除事件广播
                    try { AlarmCleared?.Invoke(this, existingAlarm); } catch { }
                }
            }
        }
    }

    public void Dispose()
    {
        _busSubscription.Dispose();
        _activeAlarms.Clear();
        _rulesByTag.Clear();
    }
}
