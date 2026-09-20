using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mal.UniversalScada.Core.Configuration;
using Mal.UniversalScada.Core.Enums;
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
    private ObservableCollection<long> _availableTagIds = new();

    [ObservableProperty]
    private ObservableCollection<TagOptionItem> _filteredAvailableTags = new();

    [ObservableProperty]
    private TagOptionItem? _selectedTagInfo;

    [ObservableProperty]
    private ObservableCollection<DeviceOptionItem> _availableDevices = new();

    private DeviceOptionItem? _selectedDeviceOption;
    public DeviceOptionItem? SelectedDeviceOption
    {
        get => _selectedDeviceOption;
        set
        {
            if (SetProperty(ref _selectedDeviceOption, value))
            {
                if (SelectedWidget is DeviceStatusWidgetViewModel devVm && value != null)
                {
                    if (!string.IsNullOrEmpty(value.DeviceId))
                    {
                        devVm.TargetDeviceId = value.DeviceId;
                        devVm.DeviceName = value.Name;
                        devVm.ChannelId = value.ChannelId;
                        devVm.Protocol = value.ProtocolType.ToString();
                        devVm.StationAddress = value.StationAddress;
                        devVm.PollIntervalMs = value.DefaultPollIntervalMs;
                        devVm.TagCount = value.TagCount;
                        if (string.IsNullOrWhiteSpace(devVm.Title) || devVm.Title.StartsWith("工位") || devVm.Title == "设备状态监视卡片")
                        {
                            devVm.Title = value.Name;
                        }
                    }
                    else
                    {
                        devVm.TargetDeviceId = string.Empty;
                        devVm.DeviceName = "未关联设备";
                    }
                    devVm.SyncPropertiesFromFields();
                }
            }
        }
    }

    private readonly List<TagNode> _allRawTags = new();

    [ObservableProperty]
    private string _statusMessage = "就绪";

    public ObservableCollection<ToolboxItemRecord> ToolboxItems { get; } = new()
    {
        new(WidgetType.TextLabel, "文本标签", "🏷️", "工位说明、静态标题或点位文本标注"),
        new(WidgetType.DisplayBox, "普通显示框", "🔲", "标准工控单行数值/文本显示框"),
        new(WidgetType.TrendChart, "实时趋势图", "📈", "模拟量实时动态波形折线图"),
        new(WidgetType.PanelContainer, "容器分组框", "📦", "工位区域分组边框与背景底板"),
        new(WidgetType.GaugeCircular, "270° 圆形仪表", "⏱️", "模拟量表盘展示，带量程刻度"),
        new(WidgetType.LevelTank, "立体液体储罐", "🛢️", "动态液位柱状指示，带百分比"),
        new(WidgetType.NumericCard, "数显科技卡片", "📟", "大号数显，带单位徽章与品质状态"),
        new(WidgetType.IoMatrix, "8路 IO 状态板", "🎛️", "8路开关量点阵矩阵，状态自感"),
        new(WidgetType.StatusLed, "工业状态指示灯", "💡", "三态高光状态灯，带运行/告警标识"),
        new(WidgetType.ControlButton, "普通按钮", "🔘", "下发置位控制指令至下位机点位"),
        new(WidgetType.Pipe, "工艺管道", "🌊", "P&ID 工业工艺管道，带动态介质流动动效"),
        new(WidgetType.Valve, "工业控制阀", "🚰", "P&ID 工业控制阀，开闭状态流道光效与反转控制"),
        new(WidgetType.Pump, "离心旋转泵", "🌀", "动力旋转泵，360°旋转叶轮动效与启停控制"),
        new(WidgetType.DeviceStatus, "设备状态卡片", "🖥️", "通信节点状态监视，展示在线/延时/协议/通道")
    };

    #region 撤销重做 (Undo / Redo) 历史栈

    private readonly Stack<List<WidgetConfig>> _undoStack = new();
    private readonly Stack<List<WidgetConfig>> _redoStack = new();
    private const int MaxHistorySteps = 50;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    public void PushHistorySnapshot()
    {
        if (SelectedView == null) return;
        var snapshot = Widgets.Select(w => w.ToConfig()).ToList();
        _undoStack.Push(snapshot);
        if (_undoStack.Count > MaxHistorySteps)
        {
            var list = _undoStack.ToList();
            list.RemoveAt(list.Count - 1);
            _undoStack.Clear();
            for (int i = list.Count - 1; i >= 0; i--) _undoStack.Push(list[i]);
        }
        _redoStack.Clear();
        NotifyHistoryChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo()
    {
        if (_undoStack.Count == 0) return;

        var current = Widgets.Select(w => w.ToConfig()).ToList();
        _redoStack.Push(current);

        var prev = _undoStack.Pop();
        RestoreFromConfigs(prev);
        StatusMessage = $"已撤销操作 (历史剩余: {_undoStack.Count} 步)";
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo()
    {
        if (_redoStack.Count == 0) return;

        var current = Widgets.Select(w => w.ToConfig()).ToList();
        _undoStack.Push(current);

        var next = _redoStack.Pop();
        RestoreFromConfigs(next);
        StatusMessage = $"已重做操作 (重做剩余: {_redoStack.Count} 步)";
    }

    private void RestoreFromConfigs(List<WidgetConfig> configs)
    {
        Widgets.Clear();
        foreach (var cfg in configs)
        {
            Widgets.Add(WidgetViewModel.FromConfig(cfg, isDesignMode: true));
        }
        SelectedWidget = Widgets.FirstOrDefault();
        if (SelectedWidget != null) SelectedWidget.IsSelected = true;
        NotifySelectionChanged();
        NotifyHistoryChanged();
    }

    #endregion

    public UiDesignerViewModel(IConfigurationService configService)
    {
        _configService = configService;

        // 订阅组件选中与删除全局事件 (支持 Ctrl 多选与多选拖拽)
        WidgetHost.WidgetSelectionRequested += OnWidgetSelectionRequested;
        WidgetHost.WidgetSelected += OnWidgetSelected;
        WidgetHost.WidgetDeleteRequested += OnWidgetDeleteRequested;
    }

    public async Task InitializeAsync()
    {
        await ReloadAvailableTagsAsync();
        await ReloadAvailableDevicesAsync();
        await ReloadViewsAsync();
    }

    public async Task ReloadAvailableTagsAsync()
    {
        try
        {
            var tags = await _configService.GetAllTagsAsync();
            _allRawTags.Clear();
            _allRawTags.AddRange(tags.OrderBy(t => t.Id));

            // 针对已经删除的点位，在组件里彻底清除对应的选择
            foreach (var w in Widgets)
            {
                if (w.PrimaryTagId > 0 &&
                    !_allRawTags.Any(t => t.Id == w.PrimaryTagId))
                {
                    w.PrimaryTagId = 0;
                    w.UpdateRuntimeValue(null);
                }
            }

            AvailableTagIds.Clear();
            AvailableTagIds.Add(0); // 支持不绑定
            foreach (var tag in _allRawTags)
            {
                AvailableTagIds.Add(tag.Id);
            }

            UpdateFilteredTagsForSelectedWidget();
            OnPrimaryTagIdChanged();
            await ReloadAvailableDevicesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载点位列表失败: {ex.Message}";
        }
    }

    public async Task ReloadAvailableDevicesAsync()
    {
        try
        {
            var devices = await _configService.GetDevicesAsync();
            AvailableDevices.Clear();
            AvailableDevices.Add(DeviceOptionItem.CreateUnbound());

            foreach (var d in devices)
            {
                int tagCount = _allRawTags.Count(t => t.DeviceId == d.DeviceId);
                AvailableDevices.Add(DeviceOptionItem.FromDevice(d, tagCount));
            }

            SyncSelectedDeviceOption();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载设备列表失败: {ex.Message}";
        }
    }

    public void SyncSelectedDeviceOption()
    {
        if (SelectedWidget is DeviceStatusWidgetViewModel devVm)
        {
            _selectedDeviceOption = AvailableDevices.FirstOrDefault(d => d.DeviceId == devVm.TargetDeviceId)
                                 ?? AvailableDevices.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedDeviceOption));
        }
        else
        {
            _selectedDeviceOption = null;
            OnPropertyChanged(nameof(SelectedDeviceOption));
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
        _undoStack.Clear();
        _redoStack.Clear();
        NotifyHistoryChanged();

        if (SelectedView == null) return;

        foreach (var wConfig in SelectedView.Widgets)
        {
            var vm = WidgetViewModel.FromConfig(wConfig, isDesignMode: true);
            Widgets.Add(vm);
        }

        SelectedWidget = Widgets.FirstOrDefault();
        if (SelectedWidget != null) SelectedWidget.IsSelected = true;
    }

    partial void OnSelectedWidgetChanged(WidgetViewModel? oldValue, WidgetViewModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnCurrentWidgetPropertyChanged;
        }

        if (newValue != null)
        {
            newValue.PropertyChanged += OnCurrentWidgetPropertyChanged;
        }

        UpdateFilteredTagsForSelectedWidget();
        OnPrimaryTagIdChanged();
        SyncSelectedDeviceOption();
    }

    private void OnCurrentWidgetPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetViewModel.PrimaryTagId))
        {
            OnPrimaryTagIdChanged();
        }
        else if (e.PropertyName == nameof(DeviceStatusProps.TargetDeviceId))
        {
            SyncSelectedDeviceOption();
        }
    }

    public bool HasMultipleSelection => Widgets.Count(w => w.IsSelected) >= 2;
    public bool HasSelection => Widgets.Any(w => w.IsSelected) || SelectedWidget != null;
    public int SelectedCount => Widgets.Count(w => w.IsSelected);
    public bool CanGroup => Widgets.Count(w => w.IsSelected) >= 2;
    public bool CanUngroup => Widgets.Any(w => w.IsSelected && !string.IsNullOrEmpty(w.GroupId));

    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasMultipleSelection));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(CanGroup));
        OnPropertyChanged(nameof(CanUngroup));
        GroupSelectedWidgetsCommand.NotifyCanExecuteChanged();
        UngroupSelectedWidgetsCommand.NotifyCanExecuteChanged();
    }

    private void OnWidgetSelectionRequested(WidgetViewModel vm, bool isCtrl)
    {
        if (isCtrl)
        {
            bool targetState = !vm.IsSelected;
            vm.IsSelected = targetState;

            // 同组成员联动切换
            if (!string.IsNullOrEmpty(vm.GroupId))
            {
                foreach (var peer in Widgets.Where(w => w.GroupId == vm.GroupId && w != vm))
                {
                    peer.IsSelected = targetState;
                }
            }

            if (vm.IsSelected)
            {
                SelectedWidget = vm;
            }
            else if (SelectedWidget == vm)
            {
                SelectedWidget = Widgets.FirstOrDefault(w => w.IsSelected);
            }
        }
        else
        {
            if (!vm.IsSelected || Widgets.Count(w => w.IsSelected) <= 1)
            {
                foreach (var w in Widgets)
                {
                    if (!string.IsNullOrEmpty(vm.GroupId))
                    {
                        w.IsSelected = (w.GroupId == vm.GroupId);
                    }
                    else
                    {
                        w.IsSelected = (w == vm);
                    }
                }
                SelectedWidget = vm;
            }
            else
            {
                if (!string.IsNullOrEmpty(vm.GroupId))
                {
                    foreach (var peer in Widgets.Where(w => w.GroupId == vm.GroupId))
                    {
                        peer.IsSelected = true;
                    }
                }
                SelectedWidget = vm;
            }
        }
        NotifySelectionChanged();
    }

    private void OnWidgetSelected(WidgetViewModel vm)
    {
        // 若点击选中的组件包含 GroupId，确保同组成员也联动保持选中
        if (!string.IsNullOrEmpty(vm.GroupId))
        {
            foreach (var peer in Widgets.Where(w => w.GroupId == vm.GroupId))
            {
                peer.IsSelected = true;
            }
        }
        NotifySelectionChanged();
    }

    private void OnWidgetDeleteRequested(WidgetViewModel vm)
    {
        DeleteWidget(vm);
    }

    #region 组件编组与解组 (Group / Ungroup)

    [RelayCommand(CanExecute = nameof(CanGroup))]
    public void GroupSelectedWidgets()
    {
        var targets = Widgets.Where(w => w.IsSelected).ToList();
        if (targets.Count < 2) return;

        PushHistorySnapshot();
        string newGroupId = "grp_" + Guid.NewGuid().ToString("N")[..8];
        foreach (var w in targets)
        {
            w.GroupId = newGroupId;
        }
        NotifySelectionChanged();
        StatusMessage = $"已将选中的 {targets.Count} 个组件编组 (组ID: {newGroupId})";
    }

    [RelayCommand(CanExecute = nameof(CanUngroup))]
    public void UngroupSelectedWidgets()
    {
        var targets = Widgets.Where(w => w.IsSelected && !string.IsNullOrEmpty(w.GroupId)).ToList();
        if (targets.Count == 0) return;

        PushHistorySnapshot();
        var targetGroupIds = targets.Select(w => w.GroupId).Distinct().ToHashSet();
        int affectedCount = 0;
        foreach (var w in Widgets)
        {
            if (w.GroupId != null && targetGroupIds.Contains(w.GroupId))
            {
                w.GroupId = null;
                affectedCount++;
            }
        }
        NotifySelectionChanged();
        StatusMessage = $"已解除选中的组件编组 (涉及 {affectedCount} 个图元)";
    }

    #endregion

    #region 画布排版与多选对齐

    [RelayCommand]
    public void AlignLeft()
    {
        var targets = Widgets.Where(w => w.IsSelected).ToList();
        if (targets.Count < 2) return;

        PushHistorySnapshot();
        double minX = targets.Min(w => w.X);
        foreach (var w in targets) w.X = minX;
        StatusMessage = $"已将 {targets.Count} 个组件左对齐 (X={minX})";
    }

    [RelayCommand]
    public void AlignRight()
    {
        var targets = Widgets.Where(w => w.IsSelected).ToList();
        if (targets.Count < 2) return;

        PushHistorySnapshot();
        double maxRight = targets.Max(w => w.X + w.Width);
        foreach (var w in targets) w.X = maxRight - w.Width;
        StatusMessage = $"已将 {targets.Count} 个组件右对齐";
    }

    [RelayCommand]
    public void AlignTop()
    {
        var targets = Widgets.Where(w => w.IsSelected).ToList();
        if (targets.Count < 2) return;

        PushHistorySnapshot();
        double minY = targets.Min(w => w.Y);
        foreach (var w in targets) w.Y = minY;
        StatusMessage = $"已将 {targets.Count} 个组件顶端对齐 (Y={minY})";
    }

    [RelayCommand]
    public void AlignBottom()
    {
        var targets = Widgets.Where(w => w.IsSelected).ToList();
        if (targets.Count < 2) return;

        PushHistorySnapshot();
        double maxBottom = targets.Max(w => w.Y + w.Height);
        foreach (var w in targets) w.Y = maxBottom - w.Height;
        StatusMessage = $"已将 {targets.Count} 个组件底端对齐";
    }

    [RelayCommand]
    public void AlignCenterHorizontal()
    {
        var targets = Widgets.Where(w => w.IsSelected).ToList();
        if (targets.Count < 2) return;

        PushHistorySnapshot();
        double avgCenterX = targets.Average(w => w.X + w.Width / 2.0);
        foreach (var w in targets) w.X = Math.Round((avgCenterX - w.Width / 2.0) / 10.0) * 10.0;
        StatusMessage = $"已将 {targets.Count} 个组件水平中线居中对齐";
    }

    [RelayCommand]
    public void AlignCenterVertical()
    {
        var targets = Widgets.Where(w => w.IsSelected).ToList();
        if (targets.Count < 2) return;

        PushHistorySnapshot();
        double avgCenterY = targets.Average(w => w.Y + w.Height / 2.0);
        foreach (var w in targets) w.Y = Math.Round((avgCenterY - w.Height / 2.0) / 10.0) * 10.0;
        StatusMessage = $"已将 {targets.Count} 个组件垂直中线居中对齐";
    }

    [RelayCommand]
    public void DistributeHorizontally()
    {
        var targets = Widgets.Where(w => w.IsSelected).ToList();
        if (targets.Count < 3)
        {
            StatusMessage = "水平等间距均分至少需要选中 3 个组件";
            return;
        }

        PushHistorySnapshot();
        var sorted = targets.OrderBy(w => w.X).ToList();
        double totalItemsWidth = sorted.Sum(w => w.Width);
        double span = (sorted.Last().X + sorted.Last().Width) - sorted.First().X;
        double totalGap = span - totalItemsWidth;
        double gap = totalGap / (sorted.Count - 1);

        double currentX = sorted[0].X;
        for (int i = 1; i < sorted.Count - 1; i++)
        {
            currentX += sorted[i - 1].Width + gap;
            sorted[i].X = Math.Round(currentX / 10.0) * 10.0;
        }
        StatusMessage = $"已将 {targets.Count} 个组件水平等间距均分排列";
    }

    [RelayCommand]
    public void DistributeVertically()
    {
        var targets = Widgets.Where(w => w.IsSelected).ToList();
        if (targets.Count < 3)
        {
            StatusMessage = "垂直等间距均分至少需要选中 3 个组件";
            return;
        }

        PushHistorySnapshot();
        var sorted = targets.OrderBy(w => w.Y).ToList();
        double totalItemsHeight = sorted.Sum(w => w.Height);
        double span = (sorted.Last().Y + sorted.Last().Height) - sorted.First().Y;
        double totalGap = span - totalItemsHeight;
        double gap = totalGap / (sorted.Count - 1);

        double currentY = sorted[0].Y;
        for (int i = 1; i < sorted.Count - 1; i++)
        {
            currentY += sorted[i - 1].Height + gap;
            sorted[i].Y = Math.Round(currentY / 10.0) * 10.0;
        }
        StatusMessage = $"已将 {targets.Count} 个组件垂直等间距均分排列";
    }

    #endregion

    #region 图层层级控制 (Z-Index)

    [RelayCommand]
    public void BringToFront()
    {
        var selected = Widgets.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0 && SelectedWidget != null) selected = new() { SelectedWidget };
        if (selected.Count == 0) return;

        PushHistorySnapshot();
        foreach (var w in selected)
        {
            Widgets.Remove(w);
            Widgets.Add(w);
        }
        StatusMessage = $"已将选中的 {selected.Count} 个组件移到最顶层";
    }

    [RelayCommand]
    public void SendToBack()
    {
        var selected = Widgets.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0 && SelectedWidget != null) selected = new() { SelectedWidget };
        if (selected.Count == 0) return;

        PushHistorySnapshot();
        for (int i = 0; i < selected.Count; i++)
        {
            Widgets.Remove(selected[i]);
            Widgets.Insert(i, selected[i]);
        }
        StatusMessage = $"已将选中的 {selected.Count} 个组件移到最底层";
    }

    [RelayCommand]
    public void BringForward()
    {
        var selected = Widgets.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0 && SelectedWidget != null) selected = new() { SelectedWidget };
        if (selected.Count == 0) return;

        PushHistorySnapshot();
        for (int i = Widgets.Count - 2; i >= 0; i--)
        {
            if (Widgets[i].IsSelected && !Widgets[i + 1].IsSelected)
            {
                Widgets.Move(i, i + 1);
            }
        }
        StatusMessage = "已将选中的组件上移一层";
    }

    [RelayCommand]
    public void SendBackward()
    {
        var selected = Widgets.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0 && SelectedWidget != null) selected = new() { SelectedWidget };
        if (selected.Count == 0) return;

        PushHistorySnapshot();
        for (int i = 1; i < Widgets.Count; i++)
        {
            if (Widgets[i].IsSelected && !Widgets[i - 1].IsSelected)
            {
                Widgets.Move(i, i - 1);
            }
        }
        StatusMessage = "已将选中的组件下移一层";
    }

    #endregion

    #region 剪贴板与批量操作

    private List<WidgetConfig> _clipboard = new();

    [RelayCommand]
    public void CopySelection()
    {
        var selected = Widgets.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0 && SelectedWidget != null) selected = new() { SelectedWidget };
        if (selected.Count == 0) return;

        _clipboard = selected.Select(w => w.ToConfig()).ToList();
        StatusMessage = $"已复制 {selected.Count} 个组件到剪贴板";
    }

    [RelayCommand]
    public void PasteSelection()
    {
        if (_clipboard.Count == 0) return;

        PushHistorySnapshot();
        foreach (var w in Widgets) w.IsSelected = false;

        var added = new List<WidgetViewModel>();
        foreach (var cfg in _clipboard)
        {
            var clone = new WidgetConfig
            {
                WidgetId = Guid.NewGuid().ToString("N")[..8],
                Type = cfg.Type,
                Title = cfg.Title + " (副本)",
                X = cfg.X + 20,
                Y = cfg.Y + 20,
                Width = cfg.Width,
                Height = cfg.Height,
                PrimaryTagId = cfg.PrimaryTagId,
                GroupId = cfg.GroupId,
                Properties = new Dictionary<string, string>(cfg.Properties),
                Action = cfg.Action != null ? new WidgetActionConfig
                {
                    ActionType = cfg.Action.ActionType,
                    TargetTagId = cfg.Action.TargetTagId,
                    Value = cfg.Action.Value,
                    ConfirmPrompt = cfg.Action.ConfirmPrompt
                } : null
            };

            var vm = WidgetViewModel.FromConfig(clone, isDesignMode: true);
            vm.IsSelected = true;
            Widgets.Add(vm);
            added.Add(vm);
        }

        SelectedWidget = added.LastOrDefault();
        NotifySelectionChanged();
        StatusMessage = $"已粘贴 {added.Count} 个组件 (偏移 +20px)";
    }

    [RelayCommand]
    public void SelectAllWidgets()
    {
        foreach (var w in Widgets) w.IsSelected = true;
        SelectedWidget = Widgets.FirstOrDefault();
        NotifySelectionChanged();
        StatusMessage = $"已全选 {Widgets.Count} 个组件";
    }

    [RelayCommand]
    public void ClearSelection()
    {
        foreach (var w in Widgets) w.IsSelected = false;
        SelectedWidget = null;
        NotifySelectionChanged();
        StatusMessage = "已取消所有选中";
    }

    [RelayCommand]
    public void DeleteSelectedWidgets()
    {
        var selected = Widgets.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0 && SelectedWidget != null) selected = new() { SelectedWidget };
        if (selected.Count == 0) return;

        PushHistorySnapshot();
        foreach (var w in selected)
        {
            Widgets.Remove(w);
        }
        SelectedWidget = Widgets.FirstOrDefault();
        if (SelectedWidget != null) SelectedWidget.IsSelected = true;
        NotifySelectionChanged();
        StatusMessage = $"已删除 {selected.Count} 个组件";
    }

    public void NudgeSelectedWidgets(double dx, double dy)
    {
        var selected = Widgets.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0 && SelectedWidget != null) selected = new() { SelectedWidget };
        foreach (var w in selected)
        {
            w.X = Math.Max(0, w.X + dx);
            w.Y = Math.Max(0, w.Y + dy);
        }
    }

    #endregion

    /// <summary>
    /// 依据当前选中的组件类型，对可用点位进行严格的数据类型兼容性过滤
    /// </summary>
    public void UpdateFilteredTagsForSelectedWidget()
    {
        FilteredAvailableTags.Clear();
        FilteredAvailableTags.Add(TagOptionItem.CreateUnbound());

        if (SelectedWidget == null)
        {
            return;
        }

        // 针对已经删除的点位，在组件里清除对应的选择
        if (SelectedWidget.PrimaryTagId > 0 &&
            !_allRawTags.Any(t => t.Id == SelectedWidget.PrimaryTagId))
        {
            SelectedWidget.PrimaryTagId = 0;
            SelectedWidget.UpdateRuntimeValue(null);
        }

        var targetType = SelectedWidget.Type;

        foreach (var tag in _allRawTags)
        {
            var opt = TagOptionItem.FromTagNode(tag);
            bool isCompatible = TagOptionItem.IsCompatibleWithWidget(targetType, tag.DataType, tag.AccessMode);

            if (isCompatible)
            {
                FilteredAvailableTags.Add(opt);
            }
        }
    }

    /// <summary>
    /// 当选中的点位发生变化时，依据点位的数据类型自动联动属性（如精度归零、单位回填、写入值约束）
    /// </summary>
    private void OnPrimaryTagIdChanged()
    {
        if (SelectedWidget == null || SelectedWidget.PrimaryTagId <= 0)
        {
            SelectedTagInfo = null;
            return;
        }

        var matched = FilteredAvailableTags.FirstOrDefault(t => t.Id == SelectedWidget.PrimaryTagId)
                   ?? _allRawTags.Where(t => t.Id == SelectedWidget.PrimaryTagId).Select(TagOptionItem.FromTagNode).FirstOrDefault();

        if (matched == null)
        {
            SelectedWidget.PrimaryTagId = 0;
            SelectedTagInfo = null;
            return;
        }

        SelectedTagInfo = matched;

        // 1. 工程单位智能回填 (当组件当前 Unit 为空时)
        if (!string.IsNullOrWhiteSpace(matched.Unit))
        {
            if (string.IsNullOrWhiteSpace(SelectedWidget.Unit))
            {
                SelectedWidget.Unit = matched.Unit;
            }
        }

        // 2. 整型点位精度自适应：整型点位无小数，小数保留位数归零
        if (matched.IsInteger)
        {
            if (SelectedWidget is CircularGaugeWidgetViewModel gauge) gauge.Decimals = 0;
            else if (SelectedWidget is NumericCardWidgetViewModel card) card.Decimals = 0;
        }

        // 3. 控制按钮写入值自适应：若是布尔量点位，限制写入值默认置为 "1"
        if (SelectedWidget is ControlButtonWidgetViewModel btn)
        {
            if (matched.IsBool)
            {
                if (btn.Props.WriteValue != "0" && btn.Props.WriteValue != "1")
                {
                    btn.Props.WriteValue = "1";
                }
            }
        }
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

        PushHistorySnapshot();

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

        if (type == WidgetType.PanelContainer)
        {
            wConfig.PrimaryTagId = 0;
        }
        else if (type == WidgetType.DeviceStatus)
        {
            wConfig.PrimaryTagId = 0;
            if (AvailableDevices.Count > 1)
            {
                var dev = AvailableDevices[1];
                wConfig.Title = dev.Name;
                wConfig.Properties["TargetDeviceId"] = dev.DeviceId;
                wConfig.Properties["DeviceName"] = dev.Name;
                wConfig.Properties["ChannelId"] = dev.ChannelId;
                wConfig.Properties["Protocol"] = dev.ProtocolType.ToString();
                wConfig.Properties["StationAddress"] = dev.StationAddress.ToString();
                wConfig.Properties["PollIntervalMs"] = dev.DefaultPollIntervalMs.ToString();
                wConfig.Properties["TagCount"] = dev.TagCount.ToString();
            }
        }
        else if (FilteredAvailableTags.Count > 1)
        {
            wConfig.PrimaryTagId = FilteredAvailableTags[1].Id;
        }
        else if (AvailableTagIds.Count > 1)
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
            PushHistorySnapshot();
            Widgets.Remove(target);
            SelectedWidget = Widgets.FirstOrDefault();
            if (SelectedWidget != null) SelectedWidget.IsSelected = true;
            StatusMessage = $"已删除组件: {target.Title}";
        }
    }

    [RelayCommand]
    public void BrowseBackgroundImage()
    {
        if (SelectedView == null) return;

        var ofd = new OpenFileDialog
        {
            Title = "选择画布背景底图 (工艺流程图/设备结构图)",
            Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.svg)|*.png;*.jpg;*.jpeg;*.bmp;*.svg|所有文件 (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            SelectedView.BackgroundImagePath = ofd.FileName;
            OnPropertyChanged(nameof(SelectedView));
            StatusMessage = $"已设置画布背景图: {System.IO.Path.GetFileName(ofd.FileName)}";
        }
    }

    [RelayCommand]
    public void ClearBackgroundImage()
    {
        if (SelectedView == null) return;

        SelectedView.BackgroundImagePath = null;
        OnPropertyChanged(nameof(SelectedView));
        StatusMessage = "已清除画布背景底图";
    }

    private static string GetDefaultTitle(WidgetType type) => type switch
    {
        WidgetType.TextLabel => "工位说明标签",
        WidgetType.DisplayBox => "实时测控显示",
        WidgetType.TrendChart => "实时趋势折线图",
        WidgetType.PanelContainer => "工位分区容器",
        WidgetType.GaugeCircular => "主轴转速 / 压力表",
        WidgetType.LevelTank => "储罐液位监测",
        WidgetType.NumericCard => "温度/流量测量项",
        WidgetType.IoMatrix => "8路数字量状态板",
        WidgetType.StatusLed => "运行就绪指示灯",
        WidgetType.ControlButton => "普通按钮",
        WidgetType.Pipe => "工艺输送管道",
        WidgetType.Valve => "工艺管路控制阀",
        WidgetType.Pump => "离心循环泵",
        WidgetType.DeviceStatus => "设备状态监视卡片",
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
                    PrimaryTagId = 0,
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
                    PrimaryTagId = 0,
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
                    PrimaryTagId = 0,
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
                    PrimaryTagId = 0,
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
                    PrimaryTagId = 0,
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
                    PrimaryTagId = 0,
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
                    PrimaryTagId = 0,
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
                    PrimaryTagId = 0,
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
                    PrimaryTagId = 0,
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
                    PrimaryTagId = 0,
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
