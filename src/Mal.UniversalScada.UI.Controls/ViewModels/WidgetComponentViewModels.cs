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
        UpdateSegmentArcs();
        ReevaluateGaugeColor();
        _props.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                if (e.PropertyName is nameof(CircularGaugeProps.MinValue) or nameof(CircularGaugeProps.MaxValue) or
                    nameof(CircularGaugeProps.LowValue) or nameof(CircularGaugeProps.HighValue))
                {
                    UpdateSegmentArcs();
                    ReevaluateGaugeColor();
                }
                else if (e.PropertyName is nameof(CircularGaugeProps.MidValue) or
                    nameof(CircularGaugeProps.LowColor) or nameof(CircularGaugeProps.MidColor) or nameof(CircularGaugeProps.HighColor) or
                    nameof(CircularGaugeProps.EnableThresholdColor))
                {
                    ReevaluateGaugeColor();
                }
                OnPropertyChanged(e.PropertyName);
            }
        };
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

        if (properties.TryGetValue("LowValue", out var lowStr) && double.TryParse(lowStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var lowVal))
            Props.LowValue = lowVal;
        else
            Props.LowValue = Props.MinValue + (Props.MaxValue - Props.MinValue) * 0.25;

        if (properties.TryGetValue("MidValue", out var midStr) && double.TryParse(midStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var midVal))
            Props.MidValue = midVal;
        else
            Props.MidValue = Props.MinValue + (Props.MaxValue - Props.MinValue) * 0.50;

        if (properties.TryGetValue("HighValue", out var hiVStr) && double.TryParse(hiVStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var hiVVal))
            Props.HighValue = hiVVal;
        else
            Props.HighValue = Props.MinValue + (Props.MaxValue - Props.MinValue) * 0.75;

        if (properties.TryGetValue("LowColor", out var lowCol))
            Props.LowColor = lowCol;

        if (properties.TryGetValue("MidColor", out var midCol))
            Props.MidColor = midCol;

        if (properties.TryGetValue("HighColor", out var hiCol))
            Props.HighColor = hiCol;

        if (properties.TryGetValue("EnableThresholdColor", out var enColorStr) && bool.TryParse(enColorStr, out var enColor))
            Props.EnableThresholdColor = enColor;

        if (properties.TryGetValue("Decimals", out var decStr) && int.TryParse(decStr, out var decVal))
            Props.Decimals = decVal;

        if (properties.TryGetValue("ColorHex", out var color))
            Props.ColorHex = color;

        UpdateSegmentArcs();
        ReevaluateGaugeColor();
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

        if (Props.LowValue.HasValue) Properties["LowValue"] = Props.LowValue.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("LowValue");

        if (Props.MidValue.HasValue) Properties["MidValue"] = Props.MidValue.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("MidValue");

        if (Props.HighValue.HasValue) Properties["HighValue"] = Props.HighValue.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("HighValue");

        Properties["LowColor"] = Props.LowColor ?? "#38BDF8";
        Properties["MidColor"] = Props.MidColor ?? "#10B981";
        Properties["HighColor"] = Props.HighColor ?? "#EF4444";
        Properties["EnableThresholdColor"] = Props.EnableThresholdColor.ToString();
    }

    public void UpdateSegmentArcs()
    {
        var min = Props.MinValue;
        var max = Props.MaxValue;
        var range = max - min;
        if (range <= 0) range = 100;

        var low = Props.LowValue ?? (min + range * 0.25);
        var high = Props.HighValue ?? (min + range * 0.75);

        low = Math.Clamp(low, min, max);
        high = Math.Clamp(high, min, max);
        if (low > high) (low, high) = (high, low);

        var ratioLow = Math.Clamp((low - min) / range, 0.0, 1.0);
        var ratioHigh = Math.Clamp((high - min) / range, 0.0, 1.0);

        const double angleStart = -135.0;
        const double angleTotal = 270.0;
        var angleLow = angleStart + ratioLow * angleTotal;
        var angleHigh = angleStart + ratioHigh * angleTotal;

        const double cx = 60.0;
        const double cy = 60.0;
        const double r = 44.0;

        Props.LowArcData = CreateArcString(cx, cy, r, angleStart, angleLow);
        Props.MidArcData = CreateArcString(cx, cy, r, angleLow, angleHigh);
        Props.HighArcData = CreateArcString(cx, cy, r, angleHigh, angleStart + angleTotal);

        // 生成 270° 主刻度线 (11 条主刻度，每 10% 也就是 27° 一条)
        var majorTicksSb = new System.Text.StringBuilder();
        const double rMajorOut = 53.0;
        const double rMajorIn = 46.5;

        for (int i = 0; i <= 10; i++)
        {
            var deg = angleStart + i * (angleTotal / 10.0);
            var rad = deg * Math.PI / 180.0;
            var x1 = cx + rMajorOut * Math.Sin(rad);
            var y1 = cy - rMajorOut * Math.Cos(rad);
            var x2 = cx + rMajorIn * Math.Sin(rad);
            var y2 = cy - rMajorIn * Math.Cos(rad);
            majorTicksSb.Append(FormattableString.Invariant($"M {x1:F2} {y1:F2} L {x2:F2} {y2:F2} "));
        }
        Props.MajorTicksPathData = majorTicksSb.ToString();

        // 生成 270° 次刻度线 (10 条次刻度，每 5% 一条，居中插空)
        var minorTicksSb = new System.Text.StringBuilder();
        const double rMinorOut = 51.0;
        const double rMinorIn = 47.0;

        for (int i = 0; i < 10; i++)
        {
            var deg = angleStart + (i + 0.5) * (angleTotal / 10.0);
            var rad = deg * Math.PI / 180.0;
            var x1 = cx + rMinorOut * Math.Sin(rad);
            var y1 = cy - rMinorOut * Math.Cos(rad);
            var x2 = cx + rMinorIn * Math.Sin(rad);
            var y2 = cy - rMinorIn * Math.Cos(rad);
            minorTicksSb.Append(FormattableString.Invariant($"M {x1:F2} {y1:F2} L {x2:F2} {y2:F2} "));
        }
        Props.MinorTicksPathData = minorTicksSb.ToString();

        // 计算 5 处关键刻度数值文本 (0%, 25%, 50%, 75%, 100%)
        Props.ScaleText0 = FormatScaleNumber(min);
        Props.ScaleText25 = FormatScaleNumber(min + range * 0.25);
        Props.ScaleText50 = FormatScaleNumber(min + range * 0.5);
        Props.ScaleText75 = FormatScaleNumber(min + range * 0.75);
        Props.ScaleText100 = FormatScaleNumber(max);
    }

    private static string FormatScaleNumber(double val)
    {
        if (Math.Abs(val - Math.Round(val)) < 0.05)
        {
            return Math.Round(val).ToString(CultureInfo.InvariantCulture);
        }
        return val.ToString("F1", CultureInfo.InvariantCulture);
    }

    private static string CreateArcString(double cx, double cy, double r, double startAngleDeg, double endAngleDeg)
    {
        if (endAngleDeg <= startAngleDeg + 0.1) return string.Empty;
        if (endAngleDeg - startAngleDeg >= 360) endAngleDeg = startAngleDeg + 359.9;

        var rad1 = startAngleDeg * Math.PI / 180.0;
        var x1 = cx + r * Math.Sin(rad1);
        var y1 = cy - r * Math.Cos(rad1);

        var rad2 = endAngleDeg * Math.PI / 180.0;
        var x2 = cx + r * Math.Sin(rad2);
        var y2 = cy - r * Math.Cos(rad2);

        int isLargeArc = (endAngleDeg - startAngleDeg) > 180.0 ? 1 : 0;

        return FormattableString.Invariant($"M {x1:F2} {y1:F2} A {r:F2} {r:F2} 0 {isLargeArc} 1 {x2:F2} {y2:F2}");
    }

    public void ReevaluateGaugeColor()
    {
        if (!Props.EnableThresholdColor)
        {
            return;
        }

        if (CurrentRawValue != null && double.TryParse(CurrentRawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            ApplyThresholdColorForValue(num);
        }
        else
        {
            Props.ColorHex = Props.MidColor ?? "#10B981";
            ColorHex = Props.ColorHex;
        }
    }

    private void ApplyThresholdColorForValue(double num)
    {
        if (!Props.EnableThresholdColor) return;

        var min = Props.MinValue;
        var max = Props.MaxValue;
        var range = max - min;
        if (range <= 0) range = 100;

        var low = Props.LowValue ?? (min + range * 0.25);
        var high = Props.HighValue ?? (min + range * 0.75);

        string targetColor;
        if (num <= low)
        {
            targetColor = Props.LowColor ?? "#38BDF8";
        }
        else if (num >= high)
        {
            targetColor = Props.HighColor ?? "#EF4444";
        }
        else
        {
            targetColor = Props.MidColor ?? "#10B981";
        }

        Props.ColorHex = targetColor;
        ColorHex = targetColor;
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
            if (Props.EnableThresholdColor)
            {
                Props.ColorHex = Props.MidColor ?? "#10B981";
                ColorHex = Props.ColorHex;
            }
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

            if (Props.EnableThresholdColor)
            {
                ApplyThresholdColorForValue(num);
            }

            bool alarm = false;
            if (Props.HighAlarm.HasValue && num >= Props.HighAlarm.Value) alarm = true;
            else if (Props.HighValue.HasValue && num >= Props.HighValue.Value) alarm = true;

            if (Props.LowAlarm.HasValue && num <= Props.LowAlarm.Value) alarm = true;
            else if (Props.LowValue.HasValue && num <= Props.LowValue.Value) alarm = true;

            if (!Props.HighAlarm.HasValue && !Props.HighValue.HasValue && !Props.LowAlarm.HasValue && !Props.LowValue.HasValue && num >= Props.MaxValue * 0.9) alarm = true;
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
        _props.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                OnPropertyChanged(e.PropertyName);
                if (e.PropertyName == nameof(TankLevelProps.Orientation))
                {
                    OnPropertyChanged(nameof(IsHorizontal));
                    AdjustDimensionsForOrientation();
                }
                else if (e.PropertyName is nameof(TankLevelProps.LowAlarm) or nameof(TankLevelProps.HighAlarm) or
                         nameof(TankLevelProps.LowColor) or nameof(TankLevelProps.MidColor) or nameof(TankLevelProps.HighColor))
                {
                    ReevaluateLiquidColor();
                }
            }
        };
    }

    public override double MinValue { get => Props.MinValue; set => Props.MinValue = value; }
    public override double MaxValue { get => Props.MaxValue; set => Props.MaxValue = value; }
    public override string Unit { get => Props.Unit; set => Props.Unit = value; }
    public override double? HighAlarm { get => Props.HighAlarm; set { Props.HighAlarm = value; OnPropertyChanged(); ReevaluateLiquidColor(); } }
    public override double? LowAlarm { get => Props.LowAlarm; set { Props.LowAlarm = value; OnPropertyChanged(); ReevaluateLiquidColor(); } }
    public override string ColorHex { get => Props.ColorHex; set => Props.ColorHex = value; }
    public override double NormalizedProgress { get => Props.NormalizedProgress; set => Props.NormalizedProgress = value; }

    public string Orientation
    {
        get => Props.Orientation;
        set
        {
            if (Props.Orientation != value)
            {
                Props.Orientation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsHorizontal));
                AdjustDimensionsForOrientation();
            }
        }
    }

    public void AdjustDimensionsForOrientation()
    {
        if (IsHorizontal)
        {
            // 切换为横向（卧式）：宽大于高，尺寸自动调整为宽 220、高 140
            if (Width < Height)
            {
                (Width, Height) = (Height, Width);
            }
            if (Width < 200) Width = 220;
            if (Height > 160) Height = 140;
        }
        else
        {
            // 切换为竖向（立式）：高大于宽，尺寸自动调整为宽 150、高 220
            if (Width > Height)
            {
                (Width, Height) = (Height, Width);
            }
            if (Width > 170) Width = 150;
            if (Height < 200) Height = 220;
        }
    }

    public bool IsHorizontal => Props.IsHorizontal;
    public string LowColor { get => Props.LowColor; set => Props.LowColor = value; }
    public string MidColor { get => Props.MidColor; set => Props.MidColor = value; }
    public string HighColor { get => Props.HighColor; set => Props.HighColor = value; }
    public string LiquidColor { get => Props.LiquidColor; set => Props.LiquidColor = value; }

    public void ReevaluateLiquidColor(double? currentVal = null)
    {
        double val;
        if (currentVal.HasValue)
        {
            val = currentVal.Value;
        }
        else if (CurrentRawValue != null && double.TryParse(CurrentRawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            val = parsed;
        }
        else
        {
            Props.LiquidColor = Props.MidColor ?? "#0284C7";
            return;
        }

        var min = Props.MinValue;
        var max = Props.MaxValue;
        var range = max - min;
        if (range <= 0) range = 100;

        var low = Props.LowAlarm ?? (min + range * 0.25);
        var high = Props.HighAlarm ?? (min + range * 0.75);

        if (low > high) (low, high) = (high, low);

        if (val <= low)
        {
            Props.LiquidColor = Props.LowColor ?? "#EAB308";
        }
        else if (val >= high)
        {
            Props.LiquidColor = Props.HighColor ?? "#EF4444";
        }
        else
        {
            Props.LiquidColor = Props.MidColor ?? "#0284C7";
        }
    }

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

        if (properties.TryGetValue("Orientation", out var orient) && !string.IsNullOrWhiteSpace(orient))
            Props.Orientation = orient;

        if (properties.TryGetValue("LowColor", out var loClr) && !string.IsNullOrWhiteSpace(loClr))
            Props.LowColor = loClr;

        if (properties.TryGetValue("MidColor", out var midClr) && !string.IsNullOrWhiteSpace(midClr))
            Props.MidColor = midClr;

        if (properties.TryGetValue("HighColor", out var hiClr) && !string.IsNullOrWhiteSpace(hiClr))
            Props.HighColor = hiClr;

        ReevaluateLiquidColor();
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["MinValue"] = Props.MinValue.ToString(CultureInfo.InvariantCulture);
        Properties["MaxValue"] = Props.MaxValue.ToString(CultureInfo.InvariantCulture);
        Properties["Unit"] = Props.Unit ?? string.Empty;
        Properties["ColorHex"] = Props.ColorHex ?? "#0284C7";
        Properties["Orientation"] = Props.Orientation ?? "Vertical";

        if (Props.HighAlarm.HasValue) Properties["HighAlarm"] = Props.HighAlarm.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("HighAlarm");

        if (Props.LowAlarm.HasValue) Properties["LowAlarm"] = Props.LowAlarm.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("LowAlarm");

        Properties.Remove("LowLevel");
        Properties.Remove("MidLevel");
        Properties.Remove("HighLevel");

        Properties["LowColor"] = Props.LowColor ?? "#EAB308";
        Properties["MidColor"] = Props.MidColor ?? "#0284C7";
        Properties["HighColor"] = Props.HighColor ?? "#EF4444";
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
            ReevaluateLiquidColor();
            return;
        }

        if (double.TryParse(rawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            FormattedValue = num.ToString("F1", CultureInfo.InvariantCulture);

            var range = Props.MaxValue - Props.MinValue;
            if (range <= 0) range = 100;

            var ratio = Math.Clamp((num - Props.MinValue) / range, 0.0, 1.0);
            Props.NormalizedProgress = ratio;

            ReevaluateLiquidColor(num);

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
        Width = 280;
        Height = 115;
        _props.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                OnPropertyChanged(e.PropertyName);
                if (e.PropertyName == nameof(IoMatrixProps.IoChannels))
                {
                    OnPropertyChanged(nameof(ChannelGroupSummary));
                    AdjustDimensionsForChannels();
                }
                else if (e.PropertyName == nameof(IoMatrixProps.DisplayFormat))
                {
                    UpdateRuntimeValue(CurrentRawValue, Quality);
                }
            }
        };
    }

    public override string ChannelGroupSummary => $"{Props.IoChannels} 点 ({(Props.IoChannels + 7) / 8} 组 Byte)";

    public void AdjustDimensionsForChannels()
    {
        (double w, double h) = Props.IoChannels switch
        {
            <= 8 => (280, 115),
            <= 16 => (280, 160),
            <= 24 => (280, 210),
            _ => (280, 260)
        };
        Width = w;
        Height = h;
        UpdateRuntimeValue(CurrentRawValue, Quality);
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
            int hexDigits = Props.IoChannels switch
            {
                <= 8 => 2,
                <= 16 => 4,
                <= 24 => 6,
                _ => 8
            };
            FormattedValue = Props.DisplayFormat?.ToUpperInvariant() switch
            {
                "BIN" => Convert.ToString(num, 2).PadLeft(Props.IoChannels, '0'),
                "DEC" => num.ToString(),
                _ => $"0x{num.ToString($"X{hexDigits}")}"
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
/// 普通按钮组件视图模型（继承自基类 WidgetViewModel，组合包含 ControlButtonProps）
/// </summary>
public partial class ControlButtonWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private ControlButtonProps _props = new();

    public override object ComponentProps => Props;

    public ControlButtonWidgetViewModel()
    {
        Type = WidgetType.ControlButton;
        Width = 100;
        Height = 36;
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

        Properties["ButtonText"] = Props.ButtonText ?? "按钮";
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
            TargetTagId = PrimaryTagId > 0 ? PrimaryTagId : null,
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
        _props.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TextLabelProps.Text))
            {
                Title = Props.Text;
                FormattedValue = Props.Text;
                OnPropertyChanged(nameof(DisplayText));
            }
            if (e.PropertyName != null) OnPropertyChanged(e.PropertyName);
        };
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Title))
            {
                if (string.IsNullOrWhiteSpace(Props.Text) || Props.Text == "文本标签 / 工位说明")
                {
                    Props.Text = Title;
                }
                FormattedValue = Props.Text;
                OnPropertyChanged(nameof(DisplayText));
            }
        };
        FormattedValue = Props.Text;
    }

    public string DisplayText => !string.IsNullOrWhiteSpace(Props.Text) ? Props.Text : (!string.IsNullOrWhiteSpace(Title) ? Title : "文本标签");

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

        if (PrimaryTagId <= 0)
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

/// <summary>
/// 工业工艺管道组件视图模型 (支持横向/纵向介质流向、流动跑马灯速度、管径与介质颜色)
/// </summary>
public partial class PipeWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private PipeProps _props = new();

    public override object ComponentProps => Props;

    public PipeWidgetViewModel()
    {
        Type = WidgetType.Pipe;
        Width = 240;
        Height = 24;
        _props.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                OnPropertyChanged(e.PropertyName);
                if (e.PropertyName == nameof(PipeProps.Orientation))
                {
                    OnPropertyChanged(nameof(IsHorizontal));
                    AdjustDimensionsForOrientation();
                }
            }
        };
    }

    public bool IsHorizontal => Props.Orientation == "Horizontal";

    public string Orientation
    {
        get => Props.Orientation;
        set
        {
            if (Props.Orientation != value)
            {
                Props.Orientation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsHorizontal));
                AdjustDimensionsForOrientation();
            }
        }
    }

    public double PipeDiameter { get => Props.PipeDiameter; set => Props.PipeDiameter = value; }
    public string PipeColor { get => Props.PipeColor; set => Props.PipeColor = value; }
    public string LiquidColor { get => Props.LiquidColor; set => Props.LiquidColor = value; }
    public string FlowDirection { get => Props.FlowDirection; set => Props.FlowDirection = value; }
    public double FlowSpeed { get => Props.FlowSpeed; set => Props.FlowSpeed = value; }
    public bool IsFlowing { get => Props.IsFlowing; set => Props.IsFlowing = value; }

    public void AdjustDimensionsForOrientation()
    {
        if (IsHorizontal)
        {
            if (Width < Height)
            {
                var prevW = Width;
                var prevH = Height;
                Width = Math.Max(prevH, 200);
                Height = Math.Min(prevW, 28);
            }
        }
        else
        {
            if (Width > Height)
            {
                var prevW = Width;
                var prevH = Height;
                Width = Math.Min(prevH, 28);
                Height = Math.Max(prevW, 200);
            }
        }
    }

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);

        if (properties.TryGetValue("Orientation", out var ori)) Props.Orientation = ori;
        if (properties.TryGetValue("PipeDiameter", out var pdStr) && double.TryParse(pdStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var pd))
            Props.PipeDiameter = pd;
        if (properties.TryGetValue("PipeColor", out var pc)) Props.PipeColor = pc;
        if (properties.TryGetValue("LiquidColor", out var lc)) Props.LiquidColor = lc;
        if (properties.TryGetValue("FlowDirection", out var fd)) Props.FlowDirection = fd;
        if (properties.TryGetValue("FlowSpeed", out var fsStr) && double.TryParse(fsStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var fs))
            Props.FlowSpeed = fs;
        if (properties.TryGetValue("IsFlowing", out var flowStr) && bool.TryParse(flowStr, out var flowVal))
            Props.IsFlowing = flowVal;

        OnPropertyChanged(nameof(IsHorizontal));
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["Orientation"] = Props.Orientation ?? "Horizontal";
        Properties["PipeDiameter"] = Props.PipeDiameter.ToString(CultureInfo.InvariantCulture);
        Properties["PipeColor"] = Props.PipeColor ?? "#1E293B";
        Properties["LiquidColor"] = Props.LiquidColor ?? "#0284C7";
        Properties["FlowDirection"] = Props.FlowDirection ?? "Forward";
        Properties["FlowSpeed"] = Props.FlowSpeed.ToString(CultureInfo.InvariantCulture);
        Properties["IsFlowing"] = Props.IsFlowing.ToString();
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (rawValue is bool b)
        {
            Props.IsFlowing = b;
        }
        else if (double.TryParse(rawValue?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            Props.IsFlowing = num > 0;
        }
    }
}

/// <summary>
/// 工业设备状态监视卡片组件视图模型（继承自基类 WidgetViewModel，组合包含 DeviceStatusProps）
/// </summary>
public partial class DeviceStatusWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private DeviceStatusProps _props = new();

    public override object ComponentProps => Props;

    public DeviceStatusWidgetViewModel()
    {
        Type = WidgetType.DeviceStatus;
        Width = 280;
        Height = 140;
        _props.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                OnPropertyChanged(e.PropertyName);
                if (e.PropertyName == nameof(DeviceStatusProps.ConnectionStatus) ||
                    e.PropertyName == nameof(DeviceStatusProps.ActiveColor) ||
                    e.PropertyName == nameof(DeviceStatusProps.OfflineColor) ||
                    e.PropertyName == nameof(DeviceStatusProps.WarningColor) ||
                    e.PropertyName == nameof(DeviceStatusProps.LatencyMs))
                {
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusBadgeText));
                    OnPropertyChanged(nameof(IsOnline));
                }
            }
        };
    }

    public string TargetDeviceId { get => Props.TargetDeviceId; set => Props.TargetDeviceId = value; }
    public string DeviceId { get => Props.TargetDeviceId; set => Props.TargetDeviceId = value; }
    public string DeviceName { get => Props.DeviceName; set => Props.DeviceName = value; }
    public string ChannelId { get => Props.ChannelId; set => Props.ChannelId = value; }
    public string Protocol { get => Props.Protocol; set => Props.Protocol = value; }
    public string ProtocolType { get => Props.Protocol; set => Props.Protocol = value; }
    public int StationAddress { get => Props.StationAddress; set => Props.StationAddress = value; }
    public int PollIntervalMs { get => Props.PollIntervalMs; set => Props.PollIntervalMs = value; }
    public string ConnectionStatus { get => Props.ConnectionStatus; set => Props.ConnectionStatus = value; }
    public int LatencyMs { get => Props.LatencyMs; set => Props.LatencyMs = value; }
    public int ResponseLatencyMs { get => Props.LatencyMs; set => Props.LatencyMs = value; }
    public int TagCount { get => Props.TagCount; set => Props.TagCount = value; }
    public bool IsCompact { get => Props.IsCompact; set => Props.IsCompact = value; }
    public override string ActiveColor { get => Props.ActiveColor; set => Props.ActiveColor = value; }
    public string OfflineColor { get => Props.OfflineColor; set => Props.OfflineColor = value; }
    public string WarningColor { get => Props.WarningColor; set => Props.WarningColor = value; }

    public bool IsOnline => string.Equals(Props.ConnectionStatus, "Online", StringComparison.OrdinalIgnoreCase);

    public string StatusColor => Props.ConnectionStatus?.ToLowerInvariant() switch
    {
        "online" => Props.ActiveColor,
        "timeout" or "warning" => Props.WarningColor,
        _ => Props.OfflineColor
    };

    public string StatusBadgeText => Props.ConnectionStatus?.ToLowerInvariant() switch
    {
        "online" => $"在线 ({Props.LatencyMs}ms)",
        "timeout" => "通信超时",
        "fault" => "设备故障",
        _ => "离线未连接"
    };

    public void ApplyDevice(DeviceNode device, int tagCount = 0)
    {
        Props.TargetDeviceId = device.DeviceId;
        Props.DeviceName = device.Name;
        Title = device.Name;
        Props.ChannelId = device.ChannelId;
        Props.Protocol = device.ProtocolType.ToString();
        Props.StationAddress = device.StationAddress;
        Props.PollIntervalMs = device.DefaultPollIntervalMs;
        Props.TagCount = tagCount;
        SyncPropertiesFromFields();
        OnPropertyChanged(string.Empty);
    }

    public void ApplyDevice(DeviceOptionItem option)
    {
        if (option == null) return;
        Props.TargetDeviceId = option.DeviceId;
        Props.DeviceName = option.Name;
        Title = string.IsNullOrWhiteSpace(option.Name) ? Title : option.Name;
        Props.ChannelId = option.ChannelId;
        Props.Protocol = option.ProtocolType.ToString();
        Props.StationAddress = option.StationAddress;
        Props.PollIntervalMs = option.DefaultPollIntervalMs;
        Props.TagCount = option.TagCount;
        SyncPropertiesFromFields();
        OnPropertyChanged(string.Empty);
    }

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);

        if (properties.TryGetValue("TargetDeviceId", out var devId)) Props.TargetDeviceId = devId;
        if (properties.TryGetValue("DeviceId", out var devId2)) Props.TargetDeviceId = devId2;
        if (properties.TryGetValue("DeviceName", out var devName)) Props.DeviceName = devName;
        if (properties.TryGetValue("ChannelId", out var chId)) Props.ChannelId = chId;
        if (properties.TryGetValue("Protocol", out var proto)) Props.Protocol = proto;
        if (properties.TryGetValue("ProtocolType", out var protoType)) Props.Protocol = protoType;
        if (properties.TryGetValue("StationAddress", out var saStr) && int.TryParse(saStr, out var sa)) Props.StationAddress = sa;
        if (properties.TryGetValue("PollIntervalMs", out var piStr) && int.TryParse(piStr, out var pi)) Props.PollIntervalMs = pi;
        if (properties.TryGetValue("ConnectionStatus", out var cs)) Props.ConnectionStatus = cs;
        if (properties.TryGetValue("LatencyMs", out var latStr) && int.TryParse(latStr, out var lat)) Props.LatencyMs = lat;
        if (properties.TryGetValue("TagCount", out var tcStr) && int.TryParse(tcStr, out var tc)) Props.TagCount = tc;
        if (properties.TryGetValue("ActiveColor", out var ac)) Props.ActiveColor = ac;
        if (properties.TryGetValue("OfflineColor", out var oc)) Props.OfflineColor = oc;
        if (properties.TryGetValue("WarningColor", out var wc)) Props.WarningColor = wc;
        if (properties.TryGetValue("IsCompact", out var compStr) && bool.TryParse(compStr, out var comp)) Props.IsCompact = comp;

        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(StatusBadgeText));
        OnPropertyChanged(nameof(IsOnline));
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["TargetDeviceId"] = Props.TargetDeviceId ?? string.Empty;
        Properties["DeviceId"] = Props.TargetDeviceId ?? string.Empty;
        Properties["DeviceName"] = Props.DeviceName ?? "未关联设备";
        Properties["ChannelId"] = Props.ChannelId ?? "--";
        Properties["Protocol"] = Props.Protocol ?? "Modbus TCP";
        Properties["ProtocolType"] = Props.Protocol ?? "Modbus TCP";
        Properties["StationAddress"] = Props.StationAddress.ToString();
        Properties["PollIntervalMs"] = Props.PollIntervalMs.ToString();
        Properties["ConnectionStatus"] = Props.ConnectionStatus ?? "Online";
        Properties["LatencyMs"] = Props.LatencyMs.ToString();
        Properties["TagCount"] = Props.TagCount.ToString();
        Properties["ActiveColor"] = Props.ActiveColor ?? "#10B981";
        Properties["OfflineColor"] = Props.OfflineColor ?? "#EF4444";
        Properties["WarningColor"] = Props.WarningColor ?? "#F59E0B";
        Properties["IsCompact"] = Props.IsCompact.ToString();
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (string.Equals(quality, "Bad", StringComparison.OrdinalIgnoreCase))
        {
            Props.ConnectionStatus = "Offline";
        }
        else if (rawValue is bool b)
        {
            Props.ConnectionStatus = b ? "Online" : "Offline";
        }
        else if (rawValue is int or long or double or float)
        {
            if (int.TryParse(rawValue.ToString(), out var num))
            {
                Props.LatencyMs = Math.Max(0, num);
                Props.ConnectionStatus = num > 1000 ? "Timeout" : "Online";
            }
        }
        else if (rawValue is string s && !string.IsNullOrWhiteSpace(s))
        {
            Props.ConnectionStatus = s;
        }

        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(StatusBadgeText));
        OnPropertyChanged(nameof(IsOnline));
    }
}

/// <summary>
/// 工业控制阀门组件视图模型
/// </summary>
public partial class ValveWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private ValveProps _props = new();

    public override object ComponentProps => Props;

    public ValveWidgetViewModel()
    {
        Type = WidgetType.Valve;
        Title = "控制阀门";
        Width = 140;
        Height = 90;
        _props.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                OnPropertyChanged(e.PropertyName);
                OnPropertyChanged(nameof(CurrentColor));
            }
        };
    }

    public string CurrentColor => Props.IsFault ? Props.FaultColor : (Props.IsOpen ? Props.OpenColor : Props.CloseColor);

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);
        if (properties.TryGetValue("Orientation", out var ori)) Props.Orientation = ori;
        if (properties.TryGetValue("IsOpen", out var openStr) && bool.TryParse(openStr, out var o)) Props.IsOpen = o;
        if (properties.TryGetValue("ValveType", out var vt)) Props.ValveType = vt;
        if (properties.TryGetValue("OpenColor", out var oc)) Props.OpenColor = oc;
        if (properties.TryGetValue("CloseColor", out var cc)) Props.CloseColor = cc;
        if (properties.TryGetValue("FaultColor", out var fc)) Props.FaultColor = fc;
        if (properties.TryGetValue("IsFault", out var faultStr) && bool.TryParse(faultStr, out var f)) Props.IsFault = f;

        Props.UpdateState(Props.IsOpen, Props.IsFault);
        OnPropertyChanged(nameof(CurrentColor));
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();
        Properties["Orientation"] = Props.Orientation ?? "Horizontal";
        Properties["IsOpen"] = Props.IsOpen.ToString();
        Properties["ValveType"] = Props.ValveType ?? "Ball";
        Properties["OpenColor"] = Props.OpenColor ?? "#10B981";
        Properties["CloseColor"] = Props.CloseColor ?? "#EF4444";
        Properties["FaultColor"] = Props.FaultColor ?? "#F59E0B";
        Properties["IsFault"] = Props.IsFault.ToString();
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (string.Equals(quality, "Bad", StringComparison.OrdinalIgnoreCase))
        {
            Props.UpdateState(Props.IsOpen, isFault: true);
            OnPropertyChanged(nameof(CurrentColor));
            return;
        }

        if (rawValue is bool b)
        {
            Props.UpdateState(b, isFault: false);
        }
        else if (rawValue is int or short or byte or long or double or float)
        {
            double val = Convert.ToDouble(rawValue);
            Props.UpdateState(val > 0.5, isFault: false);
        }
        else if (rawValue is string s)
        {
            if (s.Equals("Open", StringComparison.OrdinalIgnoreCase) || s == "1")
                Props.UpdateState(true, false);
            else if (s.Equals("Closed", StringComparison.OrdinalIgnoreCase) || s == "0")
                Props.UpdateState(false, false);
            else if (s.Equals("Fault", StringComparison.OrdinalIgnoreCase))
                Props.UpdateState(false, true);
        }

        OnPropertyChanged(nameof(CurrentColor));
    }
}

/// <summary>
/// 工业旋转离心泵组件视图模型
/// </summary>
public partial class PumpWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private PumpProps _props = new();

    public override object ComponentProps => Props;

    public PumpWidgetViewModel()
    {
        Type = WidgetType.Pump;
        Title = "离心主循环泵";
        Width = 140;
        Height = 140;
        _props.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                OnPropertyChanged(e.PropertyName);
                OnPropertyChanged(nameof(CurrentColor));
            }
        };
    }

    public string CurrentColor => Props.IsFault ? Props.FaultColor : (Props.IsRunning ? Props.RunColor : Props.StopColor);

    public override void LoadProperties(Dictionary<string, string> properties)
    {
        base.LoadProperties(properties);
        if (properties.TryGetValue("IsRunning", out var runStr) && bool.TryParse(runStr, out var r)) Props.IsRunning = r;
        if (properties.TryGetValue("IsFault", out var faultStr) && bool.TryParse(faultStr, out var f)) Props.IsFault = f;
        if (properties.TryGetValue("PumpType", out var pt)) Props.PumpType = pt;
        if (properties.TryGetValue("RunColor", out var rc)) Props.RunColor = rc;
        if (properties.TryGetValue("StopColor", out var sc)) Props.StopColor = sc;
        if (properties.TryGetValue("FaultColor", out var fc)) Props.FaultColor = fc;
        if (properties.TryGetValue("RotationSpeedRpm", out var rpmStr) && double.TryParse(rpmStr, out var rpm)) Props.RotationSpeedRpm = rpm;

        Props.UpdateState(Props.IsRunning, Props.IsFault);
        OnPropertyChanged(nameof(CurrentColor));
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();
        Properties["IsRunning"] = Props.IsRunning.ToString();
        Properties["IsFault"] = Props.IsFault.ToString();
        Properties["PumpType"] = Props.PumpType ?? "Centrifugal";
        Properties["RunColor"] = Props.RunColor ?? "#10B981";
        Properties["StopColor"] = Props.StopColor ?? "#64748B";
        Properties["FaultColor"] = Props.FaultColor ?? "#EF4444";
        Properties["RotationSpeedRpm"] = Props.RotationSpeedRpm.ToString();
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (string.Equals(quality, "Bad", StringComparison.OrdinalIgnoreCase))
        {
            Props.UpdateState(Props.IsRunning, isFault: true);
            OnPropertyChanged(nameof(CurrentColor));
            return;
        }

        if (rawValue is bool b)
        {
            Props.UpdateState(b, isFault: false);
        }
        else if (rawValue is int or short or byte or long or double or float)
        {
            double val = Convert.ToDouble(rawValue);
            Props.UpdateState(val > 0.5, isFault: false);
        }
        else if (rawValue is string s)
        {
            if (s.Equals("Run", StringComparison.OrdinalIgnoreCase) || s.Equals("Running", StringComparison.OrdinalIgnoreCase) || s == "1")
                Props.UpdateState(true, false);
            else if (s.Equals("Stop", StringComparison.OrdinalIgnoreCase) || s.Equals("Stopped", StringComparison.OrdinalIgnoreCase) || s == "0")
                Props.UpdateState(false, false);
            else if (s.Equals("Fault", StringComparison.OrdinalIgnoreCase))
                Props.UpdateState(false, true);
        }

        OnPropertyChanged(nameof(CurrentColor));
    }
}

/// <summary>
/// 180° 半圆弧形/拱形仪表盘组件视图模型（继承自基类 WidgetViewModel，持有 ArcGaugeProps）
/// </summary>
public partial class ArcGaugeWidgetViewModel : WidgetViewModel
{
    [ObservableProperty]
    private ArcGaugeProps _props = new();

    public override object ComponentProps => Props;

    public ArcGaugeWidgetViewModel()
    {
        Type = WidgetType.GaugeArc;
        Width = 200;
        Height = 140;
        UpdateScaleGeometry();

        _props.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                if (e.PropertyName is nameof(ArcGaugeProps.MinValue) or nameof(ArcGaugeProps.MaxValue) or nameof(ArcGaugeProps.Decimals))
                {
                    UpdateScaleGeometry();
                }
                OnPropertyChanged(e.PropertyName);
            }
        };
    }

    public override double MinValue { get => Props.MinValue; set => Props.MinValue = value; }
    public override double MaxValue { get => Props.MaxValue; set => Props.MaxValue = value; }
    public override string Unit { get => Props.Unit; set => Props.Unit = value; }
    public override double? HighAlarm { get => Props.HighAlarm; set => Props.HighAlarm = value; }
    public override double? LowAlarm { get => Props.LowAlarm; set => Props.LowAlarm = value; }
    public override int Decimals { get => Props.Decimals; set => Props.Decimals = value; }
    public override string ColorHex { get => Props.ColorHex; set => Props.ColorHex = value; }
    public override double NormalizedProgress { get => Props.NormalizedProgress; set => Props.NormalizedProgress = value; }

    public double NeedleAngle
    {
        get => Props.NeedleAngle;
        set => Props.NeedleAngle = value;
    }

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

        UpdateScaleGeometry();
    }

    public override void SyncPropertiesFromFields()
    {
        base.SyncPropertiesFromFields();

        Properties["MinValue"] = Props.MinValue.ToString(CultureInfo.InvariantCulture);
        Properties["MaxValue"] = Props.MaxValue.ToString(CultureInfo.InvariantCulture);
        Properties["Unit"] = Props.Unit ?? string.Empty;
        Properties["ColorHex"] = Props.ColorHex ?? "#00D2FF";
        Properties["Decimals"] = Props.Decimals.ToString();

        if (Props.HighAlarm.HasValue) Properties["HighAlarm"] = Props.HighAlarm.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("HighAlarm");

        if (Props.LowAlarm.HasValue) Properties["LowAlarm"] = Props.LowAlarm.Value.ToString(CultureInfo.InvariantCulture);
        else Properties.Remove("LowAlarm");
    }

    /// <summary>
    /// 重新计算 180° 拱形表盘主次刻度线及 6 处分度标签的坐标和数值
    /// </summary>
    public void UpdateScaleGeometry()
    {
        var min = Props.MinValue;
        var max = Props.MaxValue;
        var range = max - min;
        if (range <= 0) range = 100;

        const double cx = 100.0;
        const double cy = 102.0;
        const double rOuter = 78.0;

        // 1. 外圈 180° 拱门弧线 (-90° ~ +90°)
        var xStart = cx - rOuter;
        var xEnd = cx + rOuter;
        Props.OuterArcPathData = FormattableString.Invariant($"M {xStart:F2} {cy:F2} A {rOuter:F2} {rOuter:F2} 0 0 1 {xEnd:F2} {cy:F2}");

        // 2. 主刻度线 (5 等分，6 条主分度刻度线，-90°, -54°, -18°, +18°, +54°, +90°)
        const int divisions = 5;
        const double rMajorOut = 78.0;
        const double rMajorIn = 69.0;
        var majorSb = new System.Text.StringBuilder();

        for (int i = 0; i <= divisions; i++)
        {
            var deg = -90.0 + i * (180.0 / divisions);
            var rad = deg * Math.PI / 180.0;
            var x1 = cx + rMajorOut * Math.Sin(rad);
            var y1 = cy - rMajorOut * Math.Cos(rad);
            var x2 = cx + rMajorIn * Math.Sin(rad);
            var y2 = cy - rMajorIn * Math.Cos(rad);
            majorSb.Append(FormattableString.Invariant($"M {x1:F2} {y1:F2} L {x2:F2} {y2:F2} "));
        }
        Props.MajorTicksPathData = majorSb.ToString();

        // 3. 次刻度线 (每大格细分 5 小格，每格 4 条短刻度线，总共 20 条短刻度)
        const int minorSubDivs = 5;
        const double rMinorOut = 78.0;
        const double rMinorIn = 73.0;
        var minorSb = new System.Text.StringBuilder();

        for (int i = 0; i < divisions; i++)
        {
            for (int j = 1; j < minorSubDivs; j++)
            {
                var deg = -90.0 + (i + (double)j / minorSubDivs) * (180.0 / divisions);
                var rad = deg * Math.PI / 180.0;
                var x1 = cx + rMinorOut * Math.Sin(rad);
                var y1 = cy - rMinorOut * Math.Cos(rad);
                var x2 = cx + rMinorIn * Math.Sin(rad);
                var y2 = cy - rMinorIn * Math.Cos(rad);
                minorSb.Append(FormattableString.Invariant($"M {x1:F2} {y1:F2} L {x2:F2} {y2:F2} "));
            }
        }
        Props.MinorTicksPathData = minorSb.ToString();

        // 4. 6 个分度数值文本与坐标
        const double rLabel = 53.0;
        for (int i = 0; i <= divisions; i++)
        {
            var deg = -90.0 + i * (180.0 / divisions);
            var rad = deg * Math.PI / 180.0;
            var val = min + i * (range / divisions);
            var text = FormatScaleNumber(val, Props.Decimals);

            // 标注在内侧
            var lx = cx + rLabel * Math.Sin(rad);
            var ly = cy - rLabel * Math.Cos(rad);

            switch (i)
            {
                case 0:
                    Props.ScaleText1 = text;
                    Props.ScaleX1 = lx;
                    Props.ScaleY1 = ly;
                    break;
                case 1:
                    Props.ScaleText2 = text;
                    Props.ScaleX2 = lx;
                    Props.ScaleY2 = ly;
                    break;
                case 2:
                    Props.ScaleText3 = text;
                    Props.ScaleX3 = lx;
                    Props.ScaleY3 = ly;
                    break;
                case 3:
                    Props.ScaleText4 = text;
                    Props.ScaleX4 = lx;
                    Props.ScaleY4 = ly;
                    break;
                case 4:
                    Props.ScaleText5 = text;
                    Props.ScaleX5 = lx;
                    Props.ScaleY5 = ly;
                    break;
                case 5:
                    Props.ScaleText6 = text;
                    Props.ScaleX6 = lx;
                    Props.ScaleY6 = ly;
                    break;
            }
        }
    }

    private static string FormatScaleNumber(double val, int decimals)
    {
        if (decimals <= 0)
        {
            return Math.Round(val).ToString(CultureInfo.InvariantCulture);
        }
        var fmt = $"F{Math.Clamp(decimals, 1, 4)}";
        return val.ToString(fmt, CultureInfo.InvariantCulture);
    }

    public override void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (rawValue == null)
        {
            FormattedValue = "--";
            Props.NormalizedProgress = 0.5;
            Props.NeedleAngle = 0;
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
            Props.NeedleAngle = -90.0 + (ratio * 180.0);

            bool alarm = false;
            if (Props.HighAlarm.HasValue && num >= Props.HighAlarm.Value) alarm = true;
            if (Props.LowAlarm.HasValue && num <= Props.LowAlarm.Value) alarm = true;
            IsAlarm = alarm;
        }
        else
        {
            FormattedValue = rawValue.ToString() ?? "--";
        }
    }
}


