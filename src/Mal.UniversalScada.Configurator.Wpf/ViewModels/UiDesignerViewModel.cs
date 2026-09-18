using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mal.UniversalScada.Core.Configuration;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.UI.Controls.ViewModels;
using Mal.UniversalScada.UI.Controls.Widgets;

namespace Mal.UniversalScada.Configurator.Wpf.ViewModels;

public record ToolboxItemRecord(WidgetType Type, string Name, string Icon, string Description);

public partial class UiDesignerViewModel : ObservableObject
{
    private readonly IConfigurationService _configService;

    [ObservableProperty]
    private ObservableCollection<UiViewConfig> _views = new();

    [ObservableProperty]
    private UiViewConfig? _selectedView;

    [ObservableProperty]
    private ObservableCollection<WidgetViewModel> _widgets = new();

    [ObservableProperty]
    private WidgetViewModel? _selectedWidget;

    [ObservableProperty]
    private ObservableCollection<string> _availableTagIds = new();

    [ObservableProperty]
    private string _statusMessage = "就绪";

    public ObservableCollection<ToolboxItemRecord> ToolboxItems { get; } = new()
    {
        new(WidgetType.GaugeCircular, "270° 圆形仪表", "⏱️", "模拟量表盘展示，带量程刻度"),
        new(WidgetType.LevelTank, "立体液体储罐", "🛢️", "动态液位柱状指示，带百分比"),
        new(WidgetType.NumericCard, "数显科技卡片", "📟", "大号数显，带单位徽章与品质状态"),
        new(WidgetType.IoMatrix, "8路 IO 状态板", "🎛️", "8路开关量点阵矩阵，状态自感"),
        new(WidgetType.StatusLed, "工业状态指示灯", "💡", "三态高光状态灯，带运行/告警标识"),
        new(WidgetType.ControlButton, "工业控制按钮", "🔘", "下发置位控制指令至下位机点位")
    };

    public UiDesignerViewModel(IConfigurationService configService)
    {
        _configService = configService;

        // 订阅组件选中与删除全局事件
        WidgetHost.WidgetSelected += OnWidgetSelected;
        WidgetHost.WidgetDeleteRequested += OnWidgetDeleteRequested;
    }

    public async Task InitializeAsync()
    {
        await ReloadAvailableTagsAsync();
        await ReloadViewsAsync();
    }

    public async Task ReloadAvailableTagsAsync()
    {
        try
        {
            var tags = await _configService.GetAllTagsAsync();
            AvailableTagIds.Clear();
            AvailableTagIds.Add(string.Empty); // 支持不绑定
            foreach (var tag in tags.OrderBy(t => t.TagId))
            {
                AvailableTagIds.Add(tag.TagId);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载点位列表失败: {ex.Message}";
        }
    }

    public async Task ReloadViewsAsync()
    {
        try
        {
            var list = await _configService.GetUiViewsAsync();
            Views.Clear();

            // 若没有任何画面，默认初始化两套工业标准画面
            if (list.Count == 0)
            {
                var view1 = CreateSampleReflowOvenView();
                var view2 = CreateSampleFillingStationView();
                await _configService.SaveUiViewAsync(view1);
                await _configService.SaveUiViewAsync(view2);
                list = await _configService.GetUiViewsAsync();
            }

            foreach (var item in list)
            {
                Views.Add(item);
            }

            SelectedView = Views.FirstOrDefault(v => v.IsDefault) ?? Views.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载画面失败: {ex.Message}";
        }
    }

    partial void OnSelectedViewChanged(UiViewConfig? value)
    {
        LoadWidgetsFromSelectedView();
    }

    private void LoadWidgetsFromSelectedView()
    {
        Widgets.Clear();
        SelectedWidget = null;

        if (SelectedView == null) return;

        foreach (var wConfig in SelectedView.Widgets)
        {
            var vm = WidgetViewModel.FromConfig(wConfig, isDesignMode: true);
            Widgets.Add(vm);
        }

        SelectedWidget = Widgets.FirstOrDefault();
        if (SelectedWidget != null) SelectedWidget.IsSelected = true;
    }

    private void OnWidgetSelected(WidgetViewModel vm)
    {
        foreach (var w in Widgets)
        {
            w.IsSelected = (w == vm);
        }
        SelectedWidget = vm;
    }

    private void OnWidgetDeleteRequested(WidgetViewModel vm)
    {
        DeleteWidget(vm);
    }

    [RelayCommand]
    public async Task CreateNewViewAsync()
    {
        var count = Views.Count + 1;
        var newView = new UiViewConfig
        {
            ViewId = $"View_Custom_{count}",
            Name = $"新建监控画面 #{count}",
            CanvasWidth = 1600,
            CanvasHeight = 900,
            IsDefault = Views.Count == 0,
            Widgets = new List<WidgetConfig>
            {
                new()
                {
                    WidgetId = Guid.NewGuid().ToString("N")[..8],
                    Type = WidgetType.NumericCard,
                    Title = "系统运行参数",
                    X = 40,
                    Y = 40,
                    Width = 200,
                    Height = 130
                }
            }
        };

        await _configService.SaveUiViewAsync(newView);
        Views.Add(newView);
        SelectedView = newView;
        StatusMessage = $"成功创建画面: {newView.Name}";
    }

    [RelayCommand]
    public async Task DeleteCurrentViewAsync()
    {
        if (SelectedView == null) return;

        if (MessageBox.Show($"确定要删除画面【{SelectedView.Name}】吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var toDelete = SelectedView;
        await _configService.DeleteUiViewAsync(toDelete.ViewId);
        Views.Remove(toDelete);
        SelectedView = Views.FirstOrDefault();
        StatusMessage = $"已删除画面: {toDelete.Name}";
    }

    [RelayCommand]
    public async Task SaveCurrentViewAsync()
    {
        if (SelectedView == null) return;

        SelectedView.Widgets = Widgets.Select(w => w.ToConfig()).ToList();
        SelectedView.UpdatedTime = DateTime.Now;

        await _configService.SaveUiViewAsync(SelectedView);
        StatusMessage = $"画面【{SelectedView.Name}】保存成功 (包含 {SelectedView.Widgets.Count} 个控件)";
    }

    [RelayCommand]
    public async Task SetCurrentViewAsDefaultAsync()
    {
        if (SelectedView == null) return;

        foreach (var v in Views)
        {
            v.IsDefault = (v.ViewId == SelectedView.ViewId);
            await _configService.SaveUiViewAsync(v);
        }

        StatusMessage = $"已将【{SelectedView.Name}】设为默认启动展示画面";
    }

    [RelayCommand]
    public void AddWidget(WidgetType type)
    {
        if (SelectedView == null) return;

        double nextX = 50;
        double nextY = 50;

        if (Widgets.Count > 0)
        {
            var last = Widgets.Last();
            nextX = (last.X + last.Width + 20) % 1200;
            nextY = last.Y;
            if (nextX < 50) nextY += 150;
        }

        AddWidgetAtPosition(type, nextX, nextY);
    }

    public void AddWidgetAtPosition(WidgetType type, double x, double y)
    {
        if (SelectedView == null) return;

        var wConfig = new WidgetConfig
        {
            WidgetId = Guid.NewGuid().ToString("N")[..8],
            Type = type,
            Title = GetDefaultTitle(type),
            X = x,
            Y = y,
            Width = WidgetViewModel.GetDefaultWidth(type),
            Height = WidgetViewModel.GetDefaultHeight(type)
        };

        if (AvailableTagIds.Count > 1)
        {
            wConfig.PrimaryTagId = AvailableTagIds[1];
        }

        var vm = WidgetViewModel.FromConfig(wConfig, isDesignMode: true);
        Widgets.Add(vm);
        OnWidgetSelected(vm);
        StatusMessage = $"已添加控件: {vm.Title} 到坐标 ({x}, {y})";
    }


    [RelayCommand]
    public void DeleteWidget(WidgetViewModel? vm)
    {
        var target = vm ?? SelectedWidget;
        if (target != null)
        {
            Widgets.Remove(target);
            SelectedWidget = Widgets.FirstOrDefault();
            if (SelectedWidget != null) SelectedWidget.IsSelected = true;
            StatusMessage = $"已删除组件: {target.Title}";
        }
    }

    private static string GetDefaultTitle(WidgetType type) => type switch
    {
        WidgetType.GaugeCircular => "主轴转速 / 压力表",
        WidgetType.LevelTank => "储罐液位监测",
        WidgetType.NumericCard => "温度/流量测量项",
        WidgetType.IoMatrix => "8路数字量状态板",
        WidgetType.StatusLed => "运行就绪指示灯",
        WidgetType.ControlButton => "启动控制按键",
        _ => "监控卡片"
    };

    private static UiViewConfig CreateSampleReflowOvenView()
    {
        return new UiViewConfig
        {
            ViewId = "View_Reflow_Oven",
            Name = "回流焊多温区实时测控大屏",
            CanvasWidth = 1600,
            CanvasHeight = 900,
            IsDefault = true,
            Widgets = new List<WidgetConfig>
            {
                new()
                {
                    WidgetId = "W_OVEN_ZONE1",
                    Type = WidgetType.GaugeCircular,
                    Title = "温区1-预热区温度",
                    PrimaryTagId = "Oven_Zone1_Temp",
                    X = 40,
                    Y = 40,
                    Width = 180,
                    Height = 180,
                    Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "300", ["Unit"] = "℃" }
                },
                new()
                {
                    WidgetId = "W_OVEN_ZONE2",
                    Type = WidgetType.GaugeCircular,
                    Title = "温区2-升温区温度",
                    PrimaryTagId = "Oven_Zone2_Temp",
                    X = 240,
                    Y = 40,
                    Width = 180,
                    Height = 180,
                    Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "300", ["Unit"] = "℃" }
                },
                new()
                {
                    WidgetId = "W_OVEN_ZONE3",
                    Type = WidgetType.GaugeCircular,
                    Title = "温区3-焊接区温度",
                    PrimaryTagId = "Oven_Zone3_Temp",
                    X = 440,
                    Y = 40,
                    Width = 180,
                    Height = 180,
                    Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "300", ["Unit"] = "℃" }
                },
                new()
                {
                    WidgetId = "W_OVEN_SPEED",
                    Type = WidgetType.NumericCard,
                    Title = "传送带链速",
                    PrimaryTagId = "Conveyor_Speed",
                    X = 640,
                    Y = 40,
                    Width = 200,
                    Height = 140,
                    Properties = new() { ["Unit"] = "mm/s" }
                },
                new()
                {
                    WidgetId = "W_OVEN_STATUS",
                    Type = WidgetType.StatusLed,
                    Title = "加热管工作状态",
                    PrimaryTagId = "Heater_Status",
                    X = 860,
                    Y = 40,
                    Width = 140,
                    Height = 120
                },
                new()
                {
                    WidgetId = "W_OVEN_START_BTN",
                    Type = WidgetType.ControlButton,
                    Title = "传送带启停控制",
                    PrimaryTagId = "Conveyor_RunCmd",
                    X = 860,
                    Y = 180,
                    Width = 160,
                    Height = 90,
                    Properties = new() { ["ButtonText"] = "启动传送带", ["WriteValue"] = "1" }
                }
            }
        };
    }

    private static UiViewConfig CreateSampleFillingStationView()
    {
        return new UiViewConfig
        {
            ViewId = "View_Filling_Station",
            Name = "液体灌装车间主控台",
            CanvasWidth = 1600,
            CanvasHeight = 900,
            IsDefault = false,
            Widgets = new List<WidgetConfig>
            {
                new()
                {
                    WidgetId = "W_TANK_A",
                    Type = WidgetType.LevelTank,
                    Title = "原料储罐 A 液位",
                    PrimaryTagId = "TankA_Level",
                    X = 60,
                    Y = 40,
                    Width = 160,
                    Height = 240,
                    Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "100", ["Unit"] = "%" }
                },
                new()
                {
                    WidgetId = "W_TANK_B",
                    Type = WidgetType.LevelTank,
                    Title = "缓冲储罐 B 液位",
                    PrimaryTagId = "TankB_Level",
                    X = 250,
                    Y = 40,
                    Width = 160,
                    Height = 240,
                    Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "100", ["Unit"] = "%" }
                },
                new()
                {
                    WidgetId = "W_IO_STATUS",
                    Type = WidgetType.IoMatrix,
                    Title = "灌装阀门到位信号矩阵",
                    PrimaryTagId = "Valve_Status_Word",
                    X = 440,
                    Y = 40,
                    Width = 280,
                    Height = 150
                },
                new()
                {
                    WidgetId = "W_PUMP_BTN",
                    Type = WidgetType.ControlButton,
                    Title = "主循环泵控制",
                    PrimaryTagId = "Pump_RunCmd",
                    X = 440,
                    Y = 210,
                    Width = 160,
                    Height = 90,
                    Properties = new() { ["ButtonText"] = "开启循环泵", ["WriteValue"] = "1" }
                }
            }
        };
    }
}
