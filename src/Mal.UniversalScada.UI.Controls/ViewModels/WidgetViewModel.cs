using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.UI.Controls.ViewModels;

/// <summary>
/// SCADA 画面组件统一抽象基类。
/// 所有工业组件（仪表盘、储罐、数显、状态灯、IO矩阵、控制按钮）均继承于此基类。
/// 共用属性与运行时状态在基类中维护，组件独有的特有参数抽离到专属组合类中。
/// </summary>
public partial class WidgetViewModel : ObservableObject
{
    // ==========================================
    // 1. 所有组件共用基础属性 (由基类统一持有)
    // ==========================================

    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8];

    [ObservableProperty]
    private WidgetType _type = WidgetType.NumericCard;

    [ObservableProperty]
    private string _title = "监控组件";

    [ObservableProperty]
    private long _primaryTagId;

    [ObservableProperty]
    private double _x = 20;

    [ObservableProperty]
    private double _y = 20;

    [ObservableProperty]
    private double _width = 160;

    [ObservableProperty]
    private double _height = 160;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isDesignMode;

    [ObservableProperty]
    private string? _groupId;

    // ==========================================
    // 2. 运行时通用数据量 (动态值、品质、报警状态)
    // ==========================================

    [ObservableProperty]
    private object? _currentRawValue;

    [ObservableProperty]
    private string _formattedValue = "--";

    [ObservableProperty]
    private string _quality = "Good";

    [ObservableProperty]
    private bool _isAlarm;

    /// <summary>
    /// 底层持久化属性字典（用于与 SQLite / JSON 配置无缝交互）
    /// </summary>
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

    // ==========================================
    // 3. 专属组合属性类的多态访问接口
    // ==========================================

    /// <summary>
    /// 组件专属组合属性抽象实例
    /// </summary>
    public virtual object? ComponentProps => null;

    /// <summary>
    /// 快捷转换：圆形仪表盘专属组合属性
    /// </summary>
    public virtual CircularGaugeProps? GaugeProps => (this as CircularGaugeWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：180° 拱形仪表盘专属组合属性
    /// </summary>
    public virtual ArcGaugeProps? ArcGaugeProps => (this as ArcGaugeWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：储罐专属组合属性
    /// </summary>
    public virtual TankLevelProps? TankProps => (this as TankLevelWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：数显卡片专属组合属性
    /// </summary>
    public virtual NumericCardProps? CardProps => (this as NumericCardWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：IO 点阵矩阵专属组合属性
    /// </summary>
    public virtual IoMatrixProps? IoProps => (this as IoMatrixWidgetViewModel)?.Props;
    public virtual string? ChannelGroupSummary => (this as IoMatrixWidgetViewModel)?.ChannelGroupSummary;

    /// <summary>
    /// 快捷转换：工业指示灯专属组合属性
    /// </summary>
    public virtual StatusLedProps? LedProps => (this as StatusLedWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：控制按钮专属组合属性
    /// </summary>
    public virtual ControlButtonProps? ButtonProps => (this as ControlButtonWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：文本标签专属组合属性
    /// </summary>
    public virtual TextLabelProps? LabelProps => (this as TextLabelWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：普通显示框专属组合属性
    /// </summary>
    public virtual DisplayBoxProps? DisplayProps => (this as DisplayBoxWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：实时趋势图表专属组合属性
    /// </summary>
    public virtual TrendChartProps? ChartProps => (this as TrendChartWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：区域容器分组框专属组合属性
    /// </summary>
    public virtual PanelContainerProps? PanelProps => (this as PanelContainerWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：工艺管道专属组合属性
    /// </summary>
    public virtual PipeProps? PipeProps => (this as PipeWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：设备状态监视卡片专属组合属性
    /// </summary>
    public virtual DeviceStatusProps? DeviceProps => (this as DeviceStatusWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：工业控制阀门专属组合属性
    /// </summary>
    public virtual ValveProps? ValveProps => (this as ValveWidgetViewModel)?.Props;

    /// <summary>
    /// 快捷转换：工业离心泵专属组合属性
    /// </summary>
    public virtual PumpProps? PumpProps => (this as PumpWidgetViewModel)?.Props;

    /// <summary>
    /// 当前组件类型可用的样式与行业预设模板列表
    /// </summary>
    public IReadOnlyList<WidgetStylePreset> AvailablePresets => WidgetStylePresetCatalog.GetPresets(Type);

    private WidgetStylePreset? _selectedPreset;
    /// <summary>
    /// 当前选择的样式预设（设置时即刻套用预设模板并同步刷新）
    /// </summary>
    public WidgetStylePreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (_selectedPreset != value)
            {
                _selectedPreset = value;
                OnPropertyChanged();
                if (value != null)
                {
                    ApplyPreset(value);
                }
            }
        }
    }

    /// <summary>
    /// 一键套用样式预设模板
    /// </summary>
    public void ApplyPreset(WidgetStylePreset preset)
    {
        if (preset == null) return;
        preset.Apply(this);
        SyncPropertiesFromFields();
        OnPropertyChanged(string.Empty); // 通知全部属性更新，驱动 UI 刷新
    }

    // ==========================================
    // 4. 虚拟兼容属性（允许 XAML 或老测试代码直接平滑访问，且与组合类双向联动）
    // ==========================================

    private double _fallbackMinValue = 0;
    private double _fallbackMaxValue = 100;
    private string _fallbackUnit = string.Empty;
    private double? _fallbackHighAlarm;
    private double? _fallbackLowAlarm;
    private int _fallbackDecimals = 1;
    private string _fallbackColorHex = "#0284C7";
    private double _fallbackNormalizedProgress = 0.0;
    private double _fallbackGaugeAngle = -135;

    public virtual double MinValue
    {
        get => GaugeProps != null ? GaugeProps.MinValue : (TankProps != null ? TankProps.MinValue : _fallbackMinValue);
        set
        {
            if (GaugeProps != null) GaugeProps.MinValue = value;
            else if (TankProps != null) TankProps.MinValue = value;
            _fallbackMinValue = value;
            OnPropertyChanged();
        }
    }

    public virtual double MaxValue
    {
        get => GaugeProps != null ? GaugeProps.MaxValue : (TankProps != null ? TankProps.MaxValue : _fallbackMaxValue);
        set
        {
            if (GaugeProps != null) GaugeProps.MaxValue = value;
            else if (TankProps != null) TankProps.MaxValue = value;
            _fallbackMaxValue = value;
            OnPropertyChanged();
        }
    }

    public virtual string Unit
    {
        get => GaugeProps != null ? GaugeProps.Unit : (TankProps != null ? TankProps.Unit : (CardProps != null ? CardProps.Unit : _fallbackUnit));
        set
        {
            if (GaugeProps != null) GaugeProps.Unit = value;
            else if (TankProps != null) TankProps.Unit = value;
            else if (CardProps != null) CardProps.Unit = value;
            _fallbackUnit = value;
            OnPropertyChanged();
        }
    }

    public virtual double? HighAlarm
    {
        get => GaugeProps != null ? GaugeProps.HighAlarm : (TankProps != null ? TankProps.HighAlarm : _fallbackHighAlarm);
        set
        {
            if (GaugeProps != null) GaugeProps.HighAlarm = value;
            else if (TankProps != null) TankProps.HighAlarm = value;
            _fallbackHighAlarm = value;
            OnPropertyChanged();
        }
    }

    public virtual double? LowAlarm
    {
        get => GaugeProps != null ? GaugeProps.LowAlarm : (TankProps != null ? TankProps.LowAlarm : _fallbackLowAlarm);
        set
        {
            if (GaugeProps != null) GaugeProps.LowAlarm = value;
            else if (TankProps != null) TankProps.LowAlarm = value;
            _fallbackLowAlarm = value;
            OnPropertyChanged();
        }
    }

    public virtual int Decimals
    {
        get => GaugeProps != null ? GaugeProps.Decimals : (CardProps != null ? CardProps.Decimals : _fallbackDecimals);
        set
        {
            if (GaugeProps != null) GaugeProps.Decimals = value;
            else if (CardProps != null) CardProps.Decimals = value;
            _fallbackDecimals = value;
            OnPropertyChanged();
        }
    }

    public virtual string ColorHex
    {
        get => GaugeProps?.ColorHex ?? TankProps?.ColorHex ?? CardProps?.ColorHex ?? ButtonProps?.ColorHex ?? _fallbackColorHex;
        set
        {
            if (GaugeProps != null) GaugeProps.ColorHex = value;
            else if (TankProps != null) TankProps.ColorHex = value;
            else if (CardProps != null) CardProps.ColorHex = value;
            else if (ButtonProps != null) ButtonProps.ColorHex = value;
            _fallbackColorHex = value;
            OnPropertyChanged();
        }
    }

    public virtual double NormalizedProgress
    {
        get => GaugeProps?.NormalizedProgress ?? TankProps?.NormalizedProgress ?? _fallbackNormalizedProgress;
        set
        {
            if (GaugeProps != null) GaugeProps.NormalizedProgress = value;
            else if (TankProps != null) TankProps.NormalizedProgress = value;
            _fallbackNormalizedProgress = value;
            OnPropertyChanged();
        }
    }

    public virtual double GaugeAngle
    {
        get => GaugeProps?.GaugeAngle ?? _fallbackGaugeAngle;
        set
        {
            if (GaugeProps != null) GaugeProps.GaugeAngle = value;
            _fallbackGaugeAngle = value;
            OnPropertyChanged();
        }
    }

    public virtual string ActiveColor
    {
        get => LedProps?.ActiveColor ?? IoProps?.ActiveColor ?? "#10B981";
        set
        {
            if (LedProps != null) LedProps.ActiveColor = value;
            else if (IoProps != null) IoProps.ActiveColor = value;
            OnPropertyChanged();
        }
    }

    public virtual string InactiveColor
    {
        get => LedProps?.InactiveColor ?? IoProps?.InactiveColor ?? "#334155";
        set
        {
            if (LedProps != null) LedProps.InactiveColor = value;
            else if (IoProps != null) IoProps.InactiveColor = value;
            OnPropertyChanged();
        }
    }

    public virtual string OnText
    {
        get => LedProps?.OnText ?? "运行中";
        set
        {
            if (LedProps != null) LedProps.OnText = value;
            OnPropertyChanged();
        }
    }

    public virtual string OffText
    {
        get => LedProps?.OffText ?? "已停机";
        set
        {
            if (LedProps != null) LedProps.OffText = value;
            OnPropertyChanged();
        }
    }

    public virtual bool EnableBlink
    {
        get => LedProps?.EnableBlink ?? false;
        set
        {
            if (LedProps != null) LedProps.EnableBlink = value;
            OnPropertyChanged();
        }
    }

    public virtual bool IsActive
    {
        get => LedProps?.IsActive ?? false;
        set
        {
            if (LedProps != null) LedProps.IsActive = value;
            OnPropertyChanged();
        }
    }

    public virtual int IoChannels
    {
        get => IoProps?.IoChannels ?? 8;
        set
        {
            if (IoProps != null) IoProps.IoChannels = value;
            OnPropertyChanged();
        }
    }

    public virtual string DisplayFormat
    {
        get => IoProps?.DisplayFormat ?? "HEX";
        set
        {
            if (IoProps != null) IoProps.DisplayFormat = value;
            OnPropertyChanged();
        }
    }

    public virtual string ButtonText
    {
        get => ButtonProps?.ButtonText ?? "触发控制";
        set
        {
            if (ButtonProps != null) ButtonProps.ButtonText = value;
            OnPropertyChanged();
        }
    }

    public virtual string WriteValue
    {
        get => ButtonProps?.WriteValue ?? "1";
        set
        {
            if (ButtonProps != null) ButtonProps.WriteValue = value;
            OnPropertyChanged();
        }
    }

    public virtual string ButtonMode
    {
        get => ButtonProps?.ButtonMode ?? "DirectWrite";
        set
        {
            if (ButtonProps != null) ButtonProps.ButtonMode = value;
            OnPropertyChanged();
        }
    }

    public virtual bool RequireConfirm
    {
        get => ButtonProps?.RequireConfirm ?? false;
        set
        {
            if (ButtonProps != null) ButtonProps.RequireConfirm = value;
            OnPropertyChanged();
        }
    }

    public virtual string ConfirmMessage
    {
        get => ButtonProps?.ConfirmMessage ?? "确定要下发此项控制操作吗？";
        set
        {
            if (ButtonProps != null) ButtonProps.ConfirmMessage = value;
            OnPropertyChanged();
        }
    }

    // ==========================================
    // 5. 抽象与虚生命周期方法 (子类与组合类联动实现)
    // ==========================================

    /// <summary>
    /// 从持久化字典加载组件专有属性到组合类中
    /// </summary>
    public virtual void LoadProperties(Dictionary<string, string> properties)
    {
        if (properties.TryGetValue("MinValue", out var minStr) && double.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var minVal))
            MinValue = minVal;

        if (properties.TryGetValue("MaxValue", out var maxStr) && double.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var maxVal))
            MaxValue = maxVal;
        else if (MinValue >= MaxValue)
            MaxValue = MinValue + 100;

        if (properties.TryGetValue("Unit", out var unit))
            Unit = unit;

        if (properties.TryGetValue("HighAlarm", out var hiStr) && double.TryParse(hiStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var hiVal))
            HighAlarm = hiVal;

        if (properties.TryGetValue("LowAlarm", out var loStr) && double.TryParse(loStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var loVal))
            LowAlarm = loVal;

        if (properties.TryGetValue("Decimals", out var decStr) && int.TryParse(decStr, out var decVal))
            Decimals = decVal;

        if (properties.TryGetValue("ColorHex", out var color))
            ColorHex = color;
    }

    /// <summary>
    /// 将组合类中的专属属性回写同步到底层字典中，用于保存
    /// </summary>
    public virtual void SyncPropertiesFromFields()
    {
        Properties["MinValue"] = MinValue.ToString(CultureInfo.InvariantCulture);
        Properties["MaxValue"] = MaxValue.ToString(CultureInfo.InvariantCulture);
        Properties["Unit"] = Unit ?? string.Empty;
        Properties["ColorHex"] = ColorHex ?? "#0284C7";
        Properties["Decimals"] = Decimals.ToString();

        if (HighAlarm.HasValue) Properties["HighAlarm"] = HighAlarm.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("HighAlarm");

        if (LowAlarm.HasValue) Properties["LowAlarm"] = LowAlarm.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("LowAlarm");
    }

    /// <summary>
    /// 运行时动态更新采集点位值（驱动画面元素变化）
    /// </summary>
    public virtual void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (rawValue == null)
        {
            FormattedValue = "--";
            NormalizedProgress = 0.0;
            GaugeAngle = -135;
            IsAlarm = false;
            return;
        }

        if (rawValue is bool b)
        {
            IsActive = b;
            FormattedValue = b ? OnText : OffText;
            NormalizedProgress = b ? 1.0 : 0.0;
            GaugeAngle = b ? 135 : -135;
            IsAlarm = false;
            return;
        }

        if (double.TryParse(rawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            var fmt = $"F{Math.Clamp(Decimals, 0, 4)}";
            FormattedValue = num.ToString(fmt, CultureInfo.InvariantCulture);

            var range = MaxValue - MinValue;
            if (range <= 0) range = 100;

            var ratio = Math.Clamp((num - MinValue) / range, 0.0, 1.0);
            NormalizedProgress = ratio;
            GaugeAngle = -135.0 + (ratio * 270.0);

            bool alarm = false;
            if (HighAlarm.HasValue && num >= HighAlarm.Value) alarm = true;
            if (LowAlarm.HasValue && num <= LowAlarm.Value) alarm = true;
            if (!HighAlarm.HasValue && !LowAlarm.HasValue && num >= MaxValue * 0.9) alarm = true;
            IsAlarm = alarm;

            if (Type == WidgetType.StatusLed)
            {
                IsActive = num > 0;
                FormattedValue = IsActive ? OnText : OffText;
            }
        }
        else
        {
            FormattedValue = rawValue.ToString() ?? "--";
        }
    }

    /// <summary>
    /// 序列化生成组件配置元数据
    /// </summary>
    public virtual WidgetConfig ToConfig()
    {
        SyncPropertiesFromFields();

        var config = new WidgetConfig
        {
            WidgetId = Id,
            GroupId = GroupId,
            Type = Type,
            Title = Title,
            PrimaryTagId = PrimaryTagId,
            X = Math.Round(X, 1),
            Y = Math.Round(Y, 1),
            Width = Math.Round(Width, 1),
            Height = Math.Round(Height, 1),
            Properties = new Dictionary<string, string>(Properties)
        };

        if (Type == WidgetType.ControlButton)
        {
            config.Action = new WidgetActionConfig
            {
                ActionType = ButtonMode,
                TargetTagId = PrimaryTagId > 0 ? PrimaryTagId : null,
                Value = WriteValue,
                ConfirmPrompt = RequireConfirm ? ConfirmMessage : null
            };
        }

        return config;
    }

    // ==========================================
    // 6. 工厂方法（根据 WidgetType 实例化具体的派生组件）
    // ==========================================

    public static WidgetViewModel Create(WidgetType type, bool isDesignMode = false)
    {
        WidgetViewModel vm = type switch
        {
            WidgetType.GaugeCircular => new CircularGaugeWidgetViewModel(),
            WidgetType.GaugeArc => new ArcGaugeWidgetViewModel(),
            WidgetType.LevelTank => new TankLevelWidgetViewModel(),
            WidgetType.NumericCard => new NumericCardWidgetViewModel(),
            WidgetType.IoMatrix => new IoMatrixWidgetViewModel(),
            WidgetType.StatusLed => new StatusLedWidgetViewModel(),
            WidgetType.ControlButton => new ControlButtonWidgetViewModel(),
            WidgetType.TextLabel => new TextLabelWidgetViewModel(),
            WidgetType.DisplayBox => new DisplayBoxWidgetViewModel(),
            WidgetType.TrendChart => new TrendChartWidgetViewModel(),
            WidgetType.PanelContainer => new PanelContainerWidgetViewModel(),
            WidgetType.Pipe => new PipeWidgetViewModel(),
            WidgetType.DeviceStatus => new DeviceStatusWidgetViewModel(),
            WidgetType.Valve => new ValveWidgetViewModel(),
            WidgetType.Pump => new PumpWidgetViewModel(),
            _ => new NumericCardWidgetViewModel()
        };

        vm.Type = type;
        vm.IsDesignMode = isDesignMode;
        vm.Width = GetDefaultWidth(type);
        vm.Height = GetDefaultHeight(type);
        return vm;
    }

    public static WidgetViewModel FromConfig(WidgetConfig config, bool isDesignMode = false)
    {
        var vm = Create(config.Type, isDesignMode);
        vm.Id = config.WidgetId;
        vm.GroupId = config.GroupId;
        vm.Title = config.Title;
        vm.PrimaryTagId = config.PrimaryTagId;
        vm.X = config.X;
        vm.Y = config.Y;
        vm.Width = config.Width > 0 ? config.Width : GetDefaultWidth(config.Type);
        vm.Height = config.Height > 0 ? config.Height : GetDefaultHeight(config.Type);

        foreach (var kvp in config.Properties)
        {
            vm.Properties[kvp.Key] = kvp.Value;
        }

        vm.LoadProperties(vm.Properties);
        vm.UpdateRuntimeValue(0);

        return vm;
    }

    public static double GetDefaultWidth(WidgetType type) => type switch
    {
        WidgetType.GaugeCircular => 180,
        WidgetType.GaugeArc => 200,
        WidgetType.LevelTank => 150,
        WidgetType.NumericCard => 190,
        WidgetType.IoMatrix => 280,
        WidgetType.StatusLed => 130,
        WidgetType.ControlButton => 100,
        WidgetType.SetpointInput => 180,
        WidgetType.TextLabel => 180,
        WidgetType.DisplayBox => 200,
        WidgetType.TrendChart => 380,
        WidgetType.PanelContainer => 360,
        WidgetType.Pipe => 240,
        WidgetType.DeviceStatus => 280,
        WidgetType.Valve => 140,
        WidgetType.Pump => 140,
        _ => 160
    };

    public static double GetDefaultHeight(WidgetType type) => type switch
    {
        WidgetType.GaugeCircular => 180,
        WidgetType.GaugeArc => 140,
        WidgetType.LevelTank => 220,
        WidgetType.NumericCard => 130,
        WidgetType.IoMatrix => 115,
        WidgetType.StatusLed => 120,
        WidgetType.ControlButton => 36,
        WidgetType.SetpointInput => 100,
        WidgetType.TextLabel => 46,
        WidgetType.DisplayBox => 58,
        WidgetType.TrendChart => 220,
        WidgetType.PanelContainer => 260,
        WidgetType.Pipe => 24,
        WidgetType.DeviceStatus => 140,
        WidgetType.Valve => 90,
        WidgetType.Pump => 140,
        _ => 140
    };
}
