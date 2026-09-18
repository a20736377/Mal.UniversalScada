using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.UI.Controls.ViewModels;

/// <summary>
/// 画面组件统一视图模型（同时服务于设计器拖拽属性绑定与运行时数据刷新）。
/// </summary>
public partial class WidgetViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8];

    [ObservableProperty]
    private WidgetType _type = WidgetType.NumericCard;

    [ObservableProperty]
    private string _title = "监控组件";

    [ObservableProperty]
    private string? _primaryTagId;

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
    private double _minValue = 0;

    [ObservableProperty]
    private double _maxValue = 100;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private string _buttonText = "触发控制";

    [ObservableProperty]
    private string _writeValue = "1";

    [ObservableProperty]
    private string _colorHex = "#0284C7";

    // 运行时属性
    [ObservableProperty]
    private object? _currentRawValue;

    [ObservableProperty]
    private string _formattedValue = "--";

    [ObservableProperty]
    private double _normalizedProgress = 0.0; // 0.0 ~ 1.0 用于仪表和液位

    [ObservableProperty]
    private double _gaugeAngle = -135; // -135° ~ +135°

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isAlarm;

    [ObservableProperty]
    private string _quality = "Good";

    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

    public WidgetViewModel()
    {
    }

    public static WidgetViewModel FromConfig(WidgetConfig config, bool isDesignMode = false)
    {
        var vm = new WidgetViewModel
        {
            Id = config.WidgetId,
            Type = config.Type,
            Title = config.Title,
            PrimaryTagId = config.PrimaryTagId,
            X = config.X,
            Y = config.Y,
            Width = config.Width > 0 ? config.Width : GetDefaultWidth(config.Type),
            Height = config.Height > 0 ? config.Height : GetDefaultHeight(config.Type),
            IsDesignMode = isDesignMode
        };

        foreach (var kvp in config.Properties)
        {
            vm.Properties[kvp.Key] = kvp.Value;
        }

        if (vm.Properties.TryGetValue("MinValue", out var minStr) && double.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var minVal))
        {
            vm.MinValue = minVal;
        }

        if (vm.Properties.TryGetValue("MaxValue", out var maxStr) && double.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var maxVal))
        {
            vm.MaxValue = maxVal;
        }
        else if (vm.MinValue >= vm.MaxValue)
        {
            vm.MaxValue = vm.MinValue + 100;
        }

        if (vm.Properties.TryGetValue("Unit", out var unit))
        {
            vm.Unit = unit;
        }

        if (vm.Properties.TryGetValue("ButtonText", out var btnText))
        {
            vm.ButtonText = btnText;
        }

        if (vm.Properties.TryGetValue("WriteValue", out var wrVal))
        {
            vm.WriteValue = wrVal;
        }

        if (vm.Properties.TryGetValue("ColorHex", out var color))
        {
            vm.ColorHex = color;
        }

        vm.UpdateRuntimeValue(0);

        return vm;
    }

    public WidgetConfig ToConfig()
    {
        SyncPropertiesFromFields();

        var config = new WidgetConfig
        {
            WidgetId = Id,
            Type = Type,
            Title = Title,
            PrimaryTagId = PrimaryTagId ?? string.Empty,
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
                ActionType = "DirectWrite",
                TargetTagId = PrimaryTagId,
                Value = WriteValue
            };
        }

        return config;
    }


    public void SyncPropertiesFromFields()
    {
        Properties["MinValue"] = MinValue.ToString(CultureInfo.InvariantCulture);
        Properties["MaxValue"] = MaxValue.ToString(CultureInfo.InvariantCulture);
        Properties["Unit"] = Unit ?? string.Empty;
        Properties["ButtonText"] = ButtonText ?? "触发控制";
        Properties["WriteValue"] = WriteValue ?? "1";
        Properties["ColorHex"] = ColorHex ?? "#0284C7";
    }

    /// <summary>
    /// 运行时更新点位值
    /// </summary>
    public void UpdateRuntimeValue(object? rawValue, string quality = "Good")
    {
        CurrentRawValue = rawValue;
        Quality = quality;

        if (rawValue == null)
        {
            FormattedValue = "--";
            NormalizedProgress = 0.0;
            GaugeAngle = -135;
            IsActive = false;
            return;
        }

        if (rawValue is bool b)
        {
            IsActive = b;
            FormattedValue = b ? "ON" : "OFF";
            NormalizedProgress = b ? 1.0 : 0.0;
            GaugeAngle = b ? 135 : -135;
            return;
        }

        if (double.TryParse(rawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            FormattedValue = num.ToString("0.##");
            var range = MaxValue - MinValue;
            if (range <= 0) range = 100;

            var ratio = Math.Clamp((num - MinValue) / range, 0.0, 1.0);
            NormalizedProgress = ratio;
            GaugeAngle = -135.0 + (ratio * 270.0);
            IsActive = num > MinValue;
            IsAlarm = num >= MaxValue * 0.9;
        }
        else
        {
            FormattedValue = rawValue.ToString() ?? "--";
        }
    }

    public static double GetDefaultWidth(WidgetType type) => type switch
    {
        WidgetType.GaugeCircular => 180,
        WidgetType.LevelTank => 150,
        WidgetType.NumericCard => 180,
        WidgetType.IoMatrix => 260,
        WidgetType.StatusLed => 130,
        WidgetType.ControlButton => 140,
        WidgetType.SetpointInput => 180,
        _ => 160
    };

    public static double GetDefaultHeight(WidgetType type) => type switch
    {
        WidgetType.GaugeCircular => 180,
        WidgetType.LevelTank => 220,
        WidgetType.NumericCard => 130,
        WidgetType.IoMatrix => 140,
        WidgetType.StatusLed => 110,
        WidgetType.ControlButton => 90,
        WidgetType.SetpointInput => 100,
        _ => 140
    };
}
