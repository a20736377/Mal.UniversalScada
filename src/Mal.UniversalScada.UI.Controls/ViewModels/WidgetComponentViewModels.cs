using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.UI.Controls.ViewModels;

/// <summary>
/// 270° 圆形仪表盘组件视图模型（继承自基类 WidgetViewModel，组合包含 CircularGaugeProps）
/// </summary>
public partial class CircularGaugeWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private CircularGaugeProps _props = new();

    public override object ComponentProps => Props;

    public CircularGaugeWidgetViewModel()
    {
        Type = WidgetType.GaugeCircular;
        Width = 180;
        Height = 180;
        _props.PropertyChanged += (s, e) => { if (e.PropertyName != null) OnPropertyChanged(e.PropertyName); };
    }

    public override double MinValue { get => Props.MinValue; set => Props.MinValue = value; }
    public override double MaxValue { get => Props.MaxValue; set => Props.MaxValue = value; }
    public override string Unit { get => Props.Unit; set => Props.Unit = value; }
    public override double? HighAlarm { get => Props.HighAlarm; set => Props.HighAlarm = value; }
    public override double? LowAlarm { get => Props.LowAlarm; set => Props.LowAlarm = value; }
    public override int Decimals { get => Props.Decimals; set => Props.Decimals = value; }
    public override string ColorHex { get => Props.ColorHex; set => Props.ColorHex = value; }
    public override double NormalizedProgress { get => Props.NormalizedProgress; set => Props.NormalizedProgress = value; }
    public override double GaugeAngle { get => Props.GaugeAngle; set => Props.GaugeAngle = value; }

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);

        if (properties.TryGetValue("MinValue", out var minStr) && double.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var minVal))
            Props.MinValue = minVal;

        if (properties.TryGetValue("MaxValue", out var maxStr) && double.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var maxVal))
            Props.MaxValue = maxVal;
        else if (Props.MinValue >= Props.MaxValue)
            Props.MaxValue = Props.MinValue + 100;

        if (properties.TryGetValue("Unit", out var unit))
            Props.Unit = unit;

        if (properties.TryGetValue("HighAlarm", out var hiStr) && double.TryParse(hiStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var hiVal))
            Props.HighAlarm = hiVal;

        if (properties.TryGetValue("LowAlarm", out var loStr) && double.TryParse(loStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var loVal))
            Props.LowAlarm = loVal;

        if (properties.TryGetValue("Decimals", out var decStr) && int.TryParse(decStr, out var decVal))
            Props.Decimals = decVal;

        if (properties.TryGetValue("ColorHex", out var color))
            Props.ColorHex = color;
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["MinValue"] = Props.MinValue.ToString(CultureInfo.InvariantCulture);
        Properties["MaxValue"] = Props.MaxValue.ToString(CultureInfo.InvariantCulture);
        Properties["Unit"] = Props.Unit ?? string.Empty;
        Properties["ColorHex"] = Props.ColorHex ?? "#0284C7";
        Properties["Decimals"] = Props.Decimals.ToString();

        if (Props.HighAlarm.HasValue) Properties["HighAlarm"] = Props.HighAlarm.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("HighAlarm");

        if (Props.LowAlarm.HasValue) Properties["LowAlarm"] = Props.LowAlarm.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("LowAlarm");
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (rawValue == null)
        {
            FormattedValue = "--";
            Props.NormalizedProgress = 0.0;
            Props.GaugeAngle = -135;
            IsAlarm = false;
            return;
        }

        if (double.TryParse(rawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            var fmt = $"F{Math.Clamp(Props.Decimals, 0, 4)}";
            FormattedValue = num.ToString(fmt, CultureInfo.InvariantCulture);

            var range = Props.MaxValue - Props.MinValue;
            if (range <= 0) range = 100;

            var ratio = Math.Clamp((num - Props.MinValue) / range, 0.0, 1.0);
            Props.NormalizedProgress = ratio;
            Props.GaugeAngle = -135.0 + (ratio * 270.0);

            bool alarm = false;
            if (Props.HighAlarm.HasValue && num >= Props.HighAlarm.Value) alarm = true;
            if (Props.LowAlarm.HasValue && num <= Props.LowAlarm.Value) alarm = true;
            if (!Props.HighAlarm.HasValue && !Props.LowAlarm.HasValue && num >= Props.MaxValue * 0.9) alarm = true;
            IsAlarm = alarm;
        }
        else
        {
            FormattedValue = rawValue.ToString() ?? "--";
        }
    }
}

/// <summary>
/// 立体储罐组件视图模型（继承自基类 WidgetViewModel，组合包含 TankLevelProps）
/// </summary>
public partial class TankLevelWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private TankLevelProps _props = new();

    public override object ComponentProps => Props;

    public TankLevelWidgetViewModel()
    {
        Type = WidgetType.LevelTank;
        Width = 150;
        Height = 220;
        _props.PropertyChanged += (s, e) => { if (e.PropertyName != null) OnPropertyChanged(e.PropertyName); };
    }

    public override double MinValue { get => Props.MinValue; set => Props.MinValue = value; }
    public override double MaxValue { get => Props.MaxValue; set => Props.MaxValue = value; }
    public override string Unit { get => Props.Unit; set => Props.Unit = value; }
    public override double? HighAlarm { get => Props.HighAlarm; set => Props.HighAlarm = value; }
    public override double? LowAlarm { get => Props.LowAlarm; set => Props.LowAlarm = value; }
    public override string ColorHex { get => Props.ColorHex; set => Props.ColorHex = value; }
    public override double NormalizedProgress { get => Props.NormalizedProgress; set => Props.NormalizedProgress = value; }

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);

        if (properties.TryGetValue("MinValue", out var minStr) && double.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var minVal))
            Props.MinValue = minVal;

        if (properties.TryGetValue("MaxValue", out var maxStr) && double.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var maxVal))
            Props.MaxValue = maxVal;
        else if (Props.MinValue >= Props.MaxValue)
            Props.MaxValue = Props.MinValue + 100;

        if (properties.TryGetValue("Unit", out var unit))
            Props.Unit = unit;

        if (properties.TryGetValue("HighAlarm", out var hiStr) && double.TryParse(hiStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var hiVal))
            Props.HighAlarm = hiVal;

        if (properties.TryGetValue("LowAlarm", out var loStr) && double.TryParse(loStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var loVal))
            Props.LowAlarm = loVal;

        if (properties.TryGetValue("ColorHex", out var color))
            Props.ColorHex = color;
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["MinValue"] = Props.MinValue.ToString(CultureInfo.InvariantCulture);
        Properties["MaxValue"] = Props.MaxValue.ToString(CultureInfo.InvariantCulture);
        Properties["Unit"] = Props.Unit ?? string.Empty;
        Properties["ColorHex"] = Props.ColorHex ?? "#0284C7";

        if (Props.HighAlarm.HasValue) Properties["HighAlarm"] = Props.HighAlarm.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("HighAlarm");

        if (Props.LowAlarm.HasValue) Properties["LowAlarm"] = Props.LowAlarm.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("LowAlarm");
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (rawValue == null)
        {
            FormattedValue = "--";
            Props.NormalizedProgress = 0.0;
            IsAlarm = false;
            return;
        }

        if (double.TryParse(rawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            FormattedValue = num.ToString("F1", CultureInfo.InvariantCulture);

            var range = Props.MaxValue - Props.MinValue;
            if (range <= 0) range = 100;

            var ratio = Math.Clamp((num - Props.MinValue) / range, 0.0, 1.0);
            Props.NormalizedProgress = ratio;

            bool alarm = false;
            if (Props.HighAlarm.HasValue && num >= Props.HighAlarm.Value) alarm = true;
            if (Props.LowAlarm.HasValue && num <= Props.LowAlarm.Value) alarm = true;
            if (!Props.HighAlarm.HasValue && !Props.LowAlarm.HasValue && num >= Props.MaxValue * 0.95) alarm = true;
            IsAlarm = alarm;
        }
        else
        {
            FormattedValue = rawValue.ToString() ?? "--";
        }
    }
}

/// <summary>
/// 科技数显卡片组件视图模型（继承自基类 WidgetViewModel，组合包含 NumericCardProps）
/// </summary>
public partial class NumericCardWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private NumericCardProps _props = new();

    public override object ComponentProps => Props;

    public NumericCardWidgetViewModel()
    {
        Type = WidgetType.NumericCard;
        Width = 190;
        Height = 130;
        _props.PropertyChanged += (s, e) => { if (e.PropertyName != null) OnPropertyChanged(e.PropertyName); };
    }

    public override string Unit { get => Props.Unit; set => Props.Unit = value; }
    public override int Decimals { get => Props.Decimals; set => Props.Decimals = value; }
    public override string ColorHex { get => Props.ColorHex; set => Props.ColorHex = value; }

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);

        if (properties.TryGetValue("Unit", out var unit))
            Props.Unit = unit;

        if (properties.TryGetValue("Decimals", out var decStr) && int.TryParse(decStr, out var decVal))
            Props.Decimals = decVal;

        if (properties.TryGetValue("ColorHex", out var color))
            Props.ColorHex = color;
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["Unit"] = Props.Unit ?? string.Empty;
        Properties["ColorHex"] = Props.ColorHex ?? "#0284C7";
        Properties["Decimals"] = Props.Decimals.ToString();
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (rawValue == null)
        {
            FormattedValue = "--";
            IsAlarm = false;
            return;
        }

        if (double.TryParse(rawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            var fmt = $"F{Math.Clamp(Props.Decimals, 0, 4)}";
            FormattedValue = num.ToString(fmt, CultureInfo.InvariantCulture);
            IsAlarm = false;
        }
        else
        {
            FormattedValue = rawValue.ToString() ?? "--";
        }
    }
}

/// <summary>
/// 多路数字量 IO 状态点阵板组件视图模型（继承自基类 WidgetViewModel，组合包含 IoMatrixProps）
/// </summary>
public partial class IoMatrixWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private IoMatrixProps _props = new();

    public override object ComponentProps => Props;

    public IoMatrixWidgetViewModel()
    {
        Type = WidgetType.IoMatrix;
        Width = 260;
        Height = 140;
        _props.PropertyChanged += (s, e) => { if (e.PropertyName != null) OnPropertyChanged(e.PropertyName); };
    }

    public override int IoChannels { get => Props.IoChannels; set => Props.IoChannels = value; }
    public override string DisplayFormat { get => Props.DisplayFormat; set => Props.DisplayFormat = value; }
    public override string ActiveColor { get => Props.ActiveColor; set => Props.ActiveColor = value; }
    public override string InactiveColor { get => Props.InactiveColor; set => Props.InactiveColor = value; }

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);

        if (properties.TryGetValue("IoChannels", out var ioChStr) && int.TryParse(ioChStr, out var ioChVal))
            Props.IoChannels = ioChVal;

        if (properties.TryGetValue("DisplayFormat", out var dispFmt))
            Props.DisplayFormat = dispFmt;

        if (properties.TryGetValue("ActiveColor", out var actColor))
            Props.ActiveColor = actColor;

        if (properties.TryGetValue("InactiveColor", out var inactColor))
            Props.InactiveColor = inactColor;
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["IoChannels"] = Props.IoChannels.ToString();
        Properties["DisplayFormat"] = Props.DisplayFormat ?? "HEX";
        Properties["ActiveColor"] = Props.ActiveColor ?? "#10B981";
        Properties["InactiveColor"] = Props.InactiveColor ?? "#334155";
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (rawValue == null)
        {
            FormattedValue = "0x00";
            return;
        }

        if (long.TryParse(rawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            FormattedValue = Props.DisplayFormat?.ToUpperInvariant() switch
            {
                "BIN" => Convert.ToString(num, 2).PadLeft(Props.IoChannels, '0'),
                "DEC" => num.ToString(),
                _ => $"0x{num:X2}"
            };
        }
        else
        {
            FormattedValue = rawValue.ToString() ?? "--";
        }
    }
}

/// <summary>
/// 工业高亮状态指示灯组件视图模型（继承自基类 WidgetViewModel，组合包含 StatusLedProps）
/// </summary>
public partial class StatusLedWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private StatusLedProps _props = new();

    public override object ComponentProps => Props;

    public StatusLedWidgetViewModel()
    {
        Type = WidgetType.StatusLed;
        Width = 130;
        Height = 120;
        _props.PropertyChanged += (s, e) => { if (e.PropertyName != null) OnPropertyChanged(e.PropertyName); };
    }

    public override string ActiveColor { get => Props.ActiveColor; set => Props.ActiveColor = value; }
    public override string InactiveColor { get => Props.InactiveColor; set => Props.InactiveColor = value; }
    public override string OnText { get => Props.OnText; set => Props.OnText = value; }
    public override string OffText { get => Props.OffText; set => Props.OffText = value; }
    public override bool EnableBlink { get => Props.EnableBlink; set => Props.EnableBlink = value; }
    public override bool IsActive { get => Props.IsActive; set => Props.IsActive = value; }

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);

        if (properties.TryGetValue("ActiveColor", out var actColor))
            Props.ActiveColor = actColor;

        if (properties.TryGetValue("InactiveColor", out var inactColor))
            Props.InactiveColor = inactColor;

        if (properties.TryGetValue("OnText", out var onTxt))
            Props.OnText = onTxt;

        if (properties.TryGetValue("OffText", out var offTxt))
            Props.OffText = offTxt;

        if (properties.TryGetValue("EnableBlink", out var blinkStr) && bool.TryParse(blinkStr, out var blinkVal))
            Props.EnableBlink = blinkVal;
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["ActiveColor"] = Props.ActiveColor ?? "#10B981";
        Properties["InactiveColor"] = Props.InactiveColor ?? "#334155";
        Properties["OnText"] = Props.OnText ?? "运行中";
        Properties["OffText"] = Props.OffText ?? "已停机";
        Properties["EnableBlink"] = Props.EnableBlink.ToString();
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (rawValue == null)
        {
            Props.IsActive = false;
            FormattedValue = Props.OffText;
            return;
        }

        if (rawValue is bool b)
        {
            Props.IsActive = b;
            FormattedValue = b ? Props.OnText : Props.OffText;
            return;
        }

        if (double.TryParse(rawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            Props.IsActive = num > 0;
            FormattedValue = Props.IsActive ? Props.OnText : Props.OffText;
        }
        else
        {
            FormattedValue = rawValue.ToString() ?? "--";
        }
    }
}

/// <summary>
/// 工业控制按钮组件视图模型（继承自基类 WidgetViewModel，组合包含 ControlButtonProps）
/// </summary>
public partial class ControlButtonWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private ControlButtonProps _props = new();

    public override object ComponentProps => Props;

    public ControlButtonWidgetViewModel()
    {
        Type = WidgetType.ControlButton;
        Width = 150;
        Height = 90;
        _props.PropertyChanged += (s, e) => { if (e.PropertyName != null) OnPropertyChanged(e.PropertyName); };
    }

    public override string ButtonText { get => Props.ButtonText; set => Props.ButtonText = value; }
    public override string WriteValue { get => Props.WriteValue; set => Props.WriteValue = value; }
    public override string ButtonMode { get => Props.ButtonMode; set => Props.ButtonMode = value; }
    public override string ColorHex { get => Props.ColorHex; set => Props.ColorHex = value; }
    public override bool RequireConfirm { get => Props.RequireConfirm; set => Props.RequireConfirm = value; }
    public override string ConfirmMessage { get => Props.ConfirmMessage; set => Props.ConfirmMessage = value; }

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);

        if (properties.TryGetValue("ButtonText", out var btnText))
            Props.ButtonText = btnText;

        if (properties.TryGetValue("WriteValue", out var wrVal))
            Props.WriteValue = wrVal;

        if (properties.TryGetValue("ButtonMode", out var btnMode))
            Props.ButtonMode = btnMode;

        if (properties.TryGetValue("ColorHex", out var color))
            Props.ColorHex = color;

        if (properties.TryGetValue("RequireConfirm", out var reqConfStr) && bool.TryParse(reqConfStr, out var reqConfVal))
            Props.RequireConfirm = reqConfVal;

        if (properties.TryGetValue("ConfirmMessage", out var confMsg))
            Props.ConfirmMessage = confMsg;
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["ButtonText"] = Props.ButtonText ?? "触发控制";
        Properties["WriteValue"] = Props.WriteValue ?? "1";
        Properties["ButtonMode"] = Props.ButtonMode ?? "DirectWrite";
        Properties["ColorHex"] = Props.ColorHex ?? "#0284C7";
        Properties["RequireConfirm"] = Props.RequireConfirm.ToString();
        Properties["ConfirmMessage"] = Props.ConfirmMessage ?? "确定要下发此项控制操作吗？";
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;
        FormattedValue = rawValue?.ToString() ?? "--";
    }

    public override WidgetConfig ToConfig()
    {
        var config = base.ToConfig();
        config.Action = new WidgetActionConfig
        {
            ActionType = Props.ButtonMode,
            TargetTagId = PrimaryTagId,
            Value = Props.WriteValue,
            ConfirmPrompt = Props.RequireConfirm ? Props.ConfirmMessage : null
        };
        return config;
    }
}

/// <summary>
/// 文本标签组件视图模型 (支持静态标注与动态点位文本呈现)
/// </summary>
public partial class TextLabelWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private TextLabelProps _props = new();

    public override object ComponentProps => Props;

    public TextLabelWidgetViewModel()
    {
        Type = WidgetType.TextLabel;
        Width = 180;
        Height = 46;
        _props.PropertyChanged += (s, e) => { if (e.PropertyName != null) OnPropertyChanged(e.PropertyName); };
    }

    public string Text { get => Props.Text; set => Props.Text = value; }
    public double LabelFontSize { get => Props.FontSize; set => Props.FontSize = value; }
    public string TextColor { get => Props.TextColor; set => Props.TextColor = value; }
    public bool IsBold { get => Props.IsBold; set => Props.IsBold = value; }
    public string TextAlignment { get => Props.Alignment; set => Props.Alignment = value; }

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);

        if (properties.TryGetValue("Text", out var text)) Props.Text = text;
        if (properties.TryGetValue("FontSize", out var fsStr) && double.TryParse(fsStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var fs))
            Props.FontSize = fs;
        if (properties.TryGetValue("TextColor", out var tc)) Props.TextColor = tc;
        if (properties.TryGetValue("IsBold", out var bStr) && bool.TryParse(bStr, out var bVal)) Props.IsBold = bVal;
        if (properties.TryGetValue("Alignment", out var align)) Props.Alignment = align;
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["Text"] = Props.Text ?? string.Empty;
        Properties["FontSize"] = Props.FontSize.ToString(CultureInfo.InvariantCulture);
        Properties["TextColor"] = Props.TextColor ?? "#F8FAFC";
        Properties["IsBold"] = Props.IsBold.ToString();
        Properties["Alignment"] = Props.Alignment ?? "Left";
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (string.IsNullOrWhiteSpace(PrimaryTagId))
        {
            FormattedValue = Props.Text;
        }
        else
        {
            FormattedValue = rawValue?.ToString() ?? "--";
        }
    }
}

/// <summary>
/// 普通显示框组件视图模型 (紧凑型标准工控数显/文本框，带前缀标签、实时值、单位及边框样式)
/// </summary>
public partial class DisplayBoxWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private DisplayBoxProps _props = new();

    public override object ComponentProps => Props;

    public DisplayBoxWidgetViewModel()
    {
        Type = WidgetType.DisplayBox;
        Width = 200;
        Height = 58;
        _props.PropertyChanged += (s, e) => { if (e.PropertyName != null) OnPropertyChanged(e.PropertyName); };
    }

    public string Prefix { get => Props.Prefix; set => Props.Prefix = value; }
    public override string Unit { get => Props.Unit; set => Props.Unit = value; }
    public override int Decimals { get => Props.Decimals; set => Props.Decimals = value; }
    public string TextColor { get => Props.TextColor; set => Props.TextColor = value; }
    public string BorderColor { get => Props.BorderColor; set => Props.BorderColor = value; }
    public string BackgroundColor { get => Props.BackgroundColor; set => Props.BackgroundColor = value; }
    public string TextAlignment { get => Props.Alignment; set => Props.Alignment = value; }

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);

        if (properties.TryGetValue("Prefix", out var pfx)) Props.Prefix = pfx;
        if (properties.TryGetValue("Unit", out var unit)) Props.Unit = unit;
        if (properties.TryGetValue("Decimals", out var decStr) && int.TryParse(decStr, out var decVal)) Props.Decimals = decVal;
        if (properties.TryGetValue("TextColor", out var tc)) Props.TextColor = tc;
        if (properties.TryGetValue("BorderColor", out var bc)) Props.BorderColor = bc;
        if (properties.TryGetValue("BackgroundColor", out var bgc)) Props.BackgroundColor = bgc;
        if (properties.TryGetValue("Alignment", out var align)) Props.Alignment = align;
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["Prefix"] = Props.Prefix ?? string.Empty;
        Properties["Unit"] = Props.Unit ?? string.Empty;
        Properties["Decimals"] = Props.Decimals.ToString();
        Properties["TextColor"] = Props.TextColor ?? "#38BDF8";
        Properties["BorderColor"] = Props.BorderColor ?? "#334155";
        Properties["BackgroundColor"] = Props.BackgroundColor ?? "#0F172A";
        Properties["Alignment"] = Props.Alignment ?? "Right";
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (rawValue == null)
        {
            FormattedValue = "--";
            return;
        }

        if (rawValue is double dVal)
        {
            FormattedValue = dVal.ToString($"F{Props.Decimals}");
        }
        else if (rawValue is float fVal)
        {
            FormattedValue = fVal.ToString($"F{Props.Decimals}");
        }
        else
        {
            FormattedValue = rawValue.ToString() ?? "--";
        }
    }
}

/// <summary>
/// 实时趋势折线图组件视图模型 (实时多点采样与平滑曲线波形渲染)
/// </summary>
public partial class TrendChartWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private TrendChartProps _props = new();

    public override object ComponentProps => Props;

    private readonly List<double> _history = new();
    private const int MaxHistoryPoints = 60;

    [ObservableProperty]
    private PointCollection _trendPoints = new();

    [ObservableProperty]
    private PointCollection _fillPoints = new();

    public TrendChartWidgetViewModel()
    {
        Type = WidgetType.TrendChart;
        Width = 380;
        Height = 220;
        _props.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                OnPropertyChanged(e.PropertyName);
                if (e.PropertyName is nameof(TrendChartProps.MinValue) or nameof(TrendChartProps.MaxValue))
                {
                    RebuildChartGeometry();
                }
            }
        };

        InitSampleWave();
    }

    public override double MinValue { get => Props.MinValue; set => Props.MinValue = value; }
    public override double MaxValue { get => Props.MaxValue; set => Props.MaxValue = value; }
    public override string Unit { get => Props.Unit; set => Props.Unit = value; }
    public string LineColor { get => Props.LineColor; set => Props.LineColor = value; }
    public string FillColor { get => Props.FillColor; set => Props.FillColor = value; }
    public string GridColor { get => Props.GridColor; set => Props.GridColor = value; }
    public int TimeWindowSeconds { get => Props.TimeWindowSeconds; set => Props.TimeWindowSeconds = value; }

    private void InitSampleWave()
    {
        _history.Clear();
        double mid = (Props.MinValue + Props.MaxValue) / 2.0;
        double amp = (Props.MaxValue - Props.MinValue) * 0.35;
        if (amp <= 0) amp = 25;
        for (int i = 0; i < 30; i++)
        {
            double val = mid + Math.Sin(i * 0.35) * amp;
            _history.Add(Math.Round(val, 1));
        }
        RebuildChartGeometry();
    }

    public void AddDataPoint(double val)
    {
        _history.Add(val);
        if (_history.Count > MaxHistoryPoints)
        {
            _history.RemoveAt(0);
        }
        RebuildChartGeometry();
    }

    public void RebuildChartGeometry()
    {
        if (_history.Count < 2) return;

        double plotLeft = 46;
        double plotRight = Math.Max(plotLeft + 30, Width - 16);
        double plotTop = 32;
        double plotBottom = Math.Max(plotTop + 30, Height - 30);
        double plotWidth = plotRight - plotLeft;
        double plotHeight = plotBottom - plotTop;

        double range = Props.MaxValue - Props.MinValue;
        if (range <= 0) range = 100;

        var points = new PointCollection();
        var fill = new PointCollection();

        fill.Add(new Point(plotLeft, plotBottom));

        for (int i = 0; i < _history.Count; i++)
        {
            double ratioX = (double)i / (_history.Count - 1);
            double x = plotLeft + ratioX * plotWidth;

            double normalizedY = Math.Clamp((_history[i] - Props.MinValue) / range, 0.0, 1.0);
            double y = plotBottom - (normalizedY * plotHeight);

            var pt = new Point(x, y);
            points.Add(pt);
            fill.Add(pt);
        }

        fill.Add(new Point(plotRight, plotBottom));

        TrendPoints = points;
        FillPoints = fill;
    }

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);

        if (properties.TryGetValue("MinValue", out var minStr) && double.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var minVal))
            Props.MinValue = minVal;
        if (properties.TryGetValue("MaxValue", out var maxStr) && double.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var maxVal))
            Props.MaxValue = maxVal;
        if (properties.TryGetValue("Unit", out var unit)) Props.Unit = unit;
        if (properties.TryGetValue("LineColor", out var lc)) Props.LineColor = lc;
        if (properties.TryGetValue("FillColor", out var fc)) Props.FillColor = fc;
        if (properties.TryGetValue("GridColor", out var gc)) Props.GridColor = gc;
        if (properties.TryGetValue("TimeWindowSeconds", out var twStr) && int.TryParse(twStr, out var twVal))
            Props.TimeWindowSeconds = twVal;

        RebuildChartGeometry();
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["MinValue"] = Props.MinValue.ToString(CultureInfo.InvariantCulture);
        Properties["MaxValue"] = Props.MaxValue.ToString(CultureInfo.InvariantCulture);
        Properties["Unit"] = Props.Unit ?? string.Empty;
        Properties["LineColor"] = Props.LineColor ?? "#38BDF8";
        Properties["FillColor"] = Props.FillColor ?? "#0369A1";
        Properties["GridColor"] = Props.GridColor ?? "#1E293B";
        Properties["TimeWindowSeconds"] = Props.TimeWindowSeconds.ToString();
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (rawValue != null && double.TryParse(rawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
        {
            FormattedValue = $"{val:F1} {Props.Unit}".Trim();
            AddDataPoint(val);
        }
        else
        {
            FormattedValue = "--";
        }
    }
}

/// <summary>
/// 区域容器分组框组件视图模型 (工位边框、组件容器卡片与背景框)
/// </summary>
public partial class PanelContainerWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private PanelContainerProps _props = new();

    public override object ComponentProps => Props;

    public PanelContainerWidgetViewModel()
    {
        Type = WidgetType.PanelContainer;
        Width = 360;
        Height = 260;
        _props.PropertyChanged += (s, e) => { if (e.PropertyName != null) OnPropertyChanged(e.PropertyName); };
    }

    public string GroupTitle { get => Props.GroupTitle; set => Props.GroupTitle = value; }
    public string HeaderBgColor { get => Props.HeaderBgColor; set => Props.HeaderBgColor = value; }
    public string BorderColor { get => Props.BorderColor; set => Props.BorderColor = value; }
    public string FillColor { get => Props.FillColor; set => Props.FillColor = value; }
    public double CornerRadius { get => Props.CornerRadius; set => Props.CornerRadius = value; }
    public double BorderThickness { get => Props.BorderThickness; set => Props.BorderThickness = value; }

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);

        if (properties.TryGetValue("GroupTitle", out var gt)) Props.GroupTitle = gt;
        if (properties.TryGetValue("HeaderBgColor", out var hbg)) Props.HeaderBgColor = hbg;
        if (properties.TryGetValue("BorderColor", out var bc)) Props.BorderColor = bc;
        if (properties.TryGetValue("FillColor", out var fc)) Props.FillColor = fc;
        if (properties.TryGetValue("CornerRadius", out var crStr) && double.TryParse(crStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var crVal))
            Props.CornerRadius = crVal;
        if (properties.TryGetValue("BorderThickness", out var btStr) && double.TryParse(btStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var btVal))
            Props.BorderThickness = btVal;
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["GroupTitle"] = Props.GroupTitle ?? "工位区域分组";
        Properties["HeaderBgColor"] = Props.HeaderBgColor ?? "#1E293B";
        Properties["BorderColor"] = Props.BorderColor ?? "#38BDF8";
        Properties["FillColor"] = Props.FillColor ?? "#0A0F1D";
        Properties["CornerRadius"] = Props.CornerRadius.ToString(CultureInfo.InvariantCulture);
        Properties["BorderThickness"] = Props.BorderThickness.ToString(CultureInfo.InvariantCulture);
    }
}

