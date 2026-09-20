using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.UI.Controls.ViewModels;

/// <summary>
/// 实时报警流水浮动条 ViewModel。
/// 挂接 IAlarmEngine，驱动前端横幅报警流水的呼吸警示、数量徽章与一键确认。
/// </summary>
public partial class AlarmBannerViewModel : ObservableObject
{
    private readonly IAlarmEngine? _alarmEngine;

    [ObservableProperty]
    private ObservableCollection<AlarmEvent> _activeAlarms = new();

    [ObservableProperty]
    private AlarmEvent? _latestAlarm;

    [ObservableProperty]
    private int _unacknowledgedCount;

    [ObservableProperty]
    private bool _hasUnacknowledgedAlarms;

    [ObservableProperty]
    private bool _isExpanded;

    public AlarmBannerViewModel()
    {
        // 设计期静态模拟示范数据
        var dummy = new AlarmEvent
        {
            TagId = 101,
            Message = "1号反应釜进料压力过高，当前值 28.5 MPa (阈值: 25.0 MPa)",
            Severity = AlarmSeverity.Critical,
            TriggerValue = 28.5,
            Threshold = 25.0,
            TriggerTime = DateTime.Now,
            IsAcknowledged = false
        };
        ActiveAlarms.Add(dummy);
        LatestAlarm = dummy;
        UnacknowledgedCount = 1;
        HasUnacknowledgedAlarms = true;
    }

    public AlarmBannerViewModel(IAlarmEngine alarmEngine)
    {
        _alarmEngine = alarmEngine ?? throw new ArgumentNullException(nameof(alarmEngine));

        _alarmEngine.AlarmRaised += (s, e) =>
        {
            AppRunOnUi(() =>
            {
                var existing = ActiveAlarms.FirstOrDefault(a => a.EventId == e.EventId);
                if (existing == null)
                {
                    ActiveAlarms.Insert(0, e);
                }
                RefreshStats();
            });
        };

        _alarmEngine.AlarmCleared += (s, e) =>
        {
            AppRunOnUi(() =>
            {
                if (e.IsAcknowledged)
                {
                    var found = ActiveAlarms.FirstOrDefault(a => a.EventId == e.EventId);
                    if (found != null) ActiveAlarms.Remove(found);
                }
                RefreshStats();
            });
        };

        RefreshFromEngine();
    }

    public void RefreshFromEngine()
    {
        if (_alarmEngine == null) return;
        var list = _alarmEngine.GetActiveAlarms();
        ActiveAlarms.Clear();
        foreach (var item in list)
        {
            ActiveAlarms.Add(item);
        }
        RefreshStats();
    }

    private void RefreshStats()
    {
        LatestAlarm = ActiveAlarms.FirstOrDefault();
        UnacknowledgedCount = ActiveAlarms.Count(a => !a.IsAcknowledged);
        HasUnacknowledgedAlarms = UnacknowledgedCount > 0;
        OnPropertyChanged(nameof(StatusColor));
    }

    public string StatusColor => HasUnacknowledgedAlarms ? "#EF4444" : "#10B981";

    [RelayCommand]
    public async Task AcknowledgeAllAsync()
    {
        if (_alarmEngine != null)
        {
            await _alarmEngine.AcknowledgeAllAlarmsAsync("Operator");
            RefreshFromEngine();
        }
        else
        {
            foreach (var a in ActiveAlarms)
            {
                a.IsAcknowledged = true;
                a.AcknowledgedBy = "Operator";
                a.AcknowledgedTime = DateTime.Now;
            }
            RefreshStats();
        }
    }

    [RelayCommand]
    public void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
    }

    private static void AppRunOnUi(Action action)
    {
        if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }
}
