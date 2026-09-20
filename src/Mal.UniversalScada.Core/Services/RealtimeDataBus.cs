using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Services;

/// <summary>
/// 工业实时数据总线（Realtime Data Bus）核心实现。
/// 维护全局点位高并发线程安全内存快照池，执行变化死区（Deadband）计算与微秒级精准事件发布订阅。
/// </summary>
public class RealtimeDataBus : IRealtimeDataBus
{
    private readonly ConcurrentDictionary<string, TagValueSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TagValueSnapshot> _lastNotifiedSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TagNode> _tagMetadata = new(StringComparer.OrdinalIgnoreCase);

    // 点位单点订阅者集合 (Key: TagId)
    private readonly ConcurrentDictionary<string, List<Action<TagValueSnapshot>>> _tagSubscribers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _subLock = new();

    // 全局数据流订阅者集合 (用于报警引擎、时序归档后台工作者)
    private readonly List<Action<TagValueSnapshot>> _globalSubscribers = new();
    private readonly object _globalLock = new();

    public int RegisteredTagCount => _snapshots.Count;

    /// <summary>
    /// 注册/更新点位元数据定义（用于获取 Deadband 等策略）
    /// </summary>
    public void RegisterTag(TagNode tag)
    {
        if (tag == null || string.IsNullOrWhiteSpace(tag.TagId)) return;
        _tagMetadata[tag.TagId] = tag;
    }

    /// <summary>
    /// 批量注册点位元数据
    /// </summary>
    public void RegisterTags(IEnumerable<TagNode> tags)
    {
        if (tags == null) return;
        foreach (var tag in tags)
        {
            RegisterTag(tag);
        }
    }

    /// <inheritdoc />
    public TagValueSnapshot? GetSnapshot(string tagId)
    {
        if (string.IsNullOrWhiteSpace(tagId)) return null;
        return _snapshots.TryGetValue(tagId, out var snapshot) ? snapshot : null;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, TagValueSnapshot> GetAllSnapshots()
    {
        return _snapshots;
    }

    /// <inheritdoc />
    public void PublishSnapshot(TagValueSnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.TagId)) return;

        bool isSignificantChange = true;

        // 依据“上一次触发有效广播通知的快照”进行死区比较（避免缓慢漂移因每次微小变动而被吞噬）
        if (_lastNotifiedSnapshots.TryGetValue(snapshot.TagId, out var lastNotified))
        {
            isSignificantChange = CheckIfSignificantChange(lastNotified, snapshot);
        }

        // 无论是否触发广播，内存快照池始终记录最新实时值供直接查询
        _snapshots[snapshot.TagId] = snapshot;

        // 如果超出死区或发生重要变化，广播通知并更新基准通知快照
        if (isSignificantChange)
        {
            _lastNotifiedSnapshots[snapshot.TagId] = snapshot;
            NotifySubscribers(snapshot);
        }
    }

    /// <inheritdoc />
    public void PublishSnapshots(IEnumerable<TagValueSnapshot> snapshots)
    {
        if (snapshots == null) return;
        foreach (var snapshot in snapshots)
        {
            PublishSnapshot(snapshot);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(string tagId, Action<TagValueSnapshot> handler)
    {
        if (string.IsNullOrWhiteSpace(tagId) || handler == null)
            return EmptyDisposable.Instance;

        lock (_subLock)
        {
            var list = _tagSubscribers.GetOrAdd(tagId, _ => new List<Action<TagValueSnapshot>>());
            list.Add(handler);
        }

        // 如果当前已有快照，立即补发一次最新状态
        if (_snapshots.TryGetValue(tagId, out var current))
        {
            try { handler(current); } catch { }
        }

        return new SubscriptionToken(() =>
        {
            lock (_subLock)
            {
                if (_tagSubscribers.TryGetValue(tagId, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0)
                    {
                        _tagSubscribers.TryRemove(tagId, out _);
                    }
                }
            }
        });
    }

    /// <inheritdoc />
    public IDisposable SubscribeAll(Action<TagValueSnapshot> handler)
    {
        if (handler == null) return EmptyDisposable.Instance;

        lock (_globalLock)
        {
            _globalSubscribers.Add(handler);
        }

        return new SubscriptionToken(() =>
        {
            lock (_globalLock)
            {
                _globalSubscribers.Remove(handler);
            }
        });
    }

    /// <summary>
    /// 广播通知单点与全局订阅者
    /// </summary>
    private void NotifySubscribers(TagValueSnapshot snapshot)
    {
        // 1. 通知该点位的特定订阅者
        Action<TagValueSnapshot>[]? targetHandlers = null;
        lock (_subLock)
        {
            if (_tagSubscribers.TryGetValue(snapshot.TagId, out var list) && list.Count > 0)
            {
                targetHandlers = list.ToArray();
            }
        }

        if (targetHandlers != null)
        {
            foreach (var h in targetHandlers)
            {
                try { h(snapshot); } catch { /* 隔离单个订阅者异常 */ }
            }
        }

        // 2. 通知全局订阅者
        Action<TagValueSnapshot>[]? globalHandlers = null;
        lock (_globalLock)
        {
            if (_globalSubscribers.Count > 0)
            {
                globalHandlers = _globalSubscribers.ToArray();
            }
        }

        if (globalHandlers != null)
        {
            foreach (var gh in globalHandlers)
            {
                try { gh(snapshot); } catch { /* 隔离全局订阅者异常 */ }
            }
        }
    }

    /// <summary>
    /// 评估是否超出死区阈值或发生显著品质/状态变动
    /// </summary>
    private bool CheckIfSignificantChange(TagValueSnapshot oldSnapshot, TagValueSnapshot newSnapshot)
    {
        // 1. 数据品质发生变化（例如 Good -> Bad 或 CommFailure -> Good），必须广播
        if (oldSnapshot.Quality != newSnapshot.Quality)
            return true;

        // 2. 两个值均为空则无实质变化
        if (oldSnapshot.Value == null && newSnapshot.Value == null)
            return false;

        if (oldSnapshot.Value == null || newSnapshot.Value == null)
            return true;

        // 3. 布尔或字符串类型，直接判断值相等性
        if (oldSnapshot.Value is bool bOld && newSnapshot.Value is bool bNew)
            return bOld != bNew;

        if (oldSnapshot.Value is string sOld && newSnapshot.Value is string sNew)
            return !string.Equals(sOld, sNew, StringComparison.Ordinal);

        // 4. 模拟量数值型：应用 Deadband
        double deadband = 0.0;
        if (_tagMetadata.TryGetValue(newSnapshot.TagId, out var meta) && meta.Deadband > 0)
        {
            deadband = meta.Deadband;
        }

        try
        {
            double oldVal = Convert.ToDouble(oldSnapshot.Value);
            double newVal = Convert.ToDouble(newSnapshot.Value);

            if (deadband > 0)
            {
                return Math.Abs(newVal - oldVal) >= deadband;
            }

            // 即使未设置 deadband，也过滤极微小浮点抖动 (1e-6)
            return Math.Abs(newVal - oldVal) > 1e-6;
        }
        catch
        {
            return !Equals(oldSnapshot.Value, newSnapshot.Value);
        }
    }

    private sealed class SubscriptionToken : IDisposable
    {
        private Action? _unsubscribe;

        public SubscriptionToken(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            var act = Interlocked.Exchange(ref _unsubscribe, null);
            act?.Invoke();
        }
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();
        public void Dispose() { }
    }
}
