using System;
using System.Globalization;
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
