using CommunityToolkit.Mvvm.ComponentModel;

namespace Mal.UniversalScada.UI.Controls.ViewModels;

/// <summary>
/// 270° 圆形仪表盘专属组合属性类（包含量程、告警阈值、表盘角度及颜色）
/// </summary>
public partial class CircularGaugeProps : ObservableObject
{
    [ObservableProperty]
    private double _minValue = 0;

    [ObservableProperty]
    private double _maxValue = 100;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private double? _highAlarm;

    [ObservableProperty]
    private double? _lowAlarm;

    [ObservableProperty]
    private int _decimals = 1;

    [ObservableProperty]
    private string _colorHex = "#0284C7";

    [ObservableProperty]
    private double _normalizedProgress = 0.0;

    [ObservableProperty]
    private double _gaugeAngle = -135; // -135° ~ +135°
}

/// <summary>
/// 立体液体储罐专属组合属性类（包含容积范围、溢出/干涸报警、液体高度比率及颜色）
/// </summary>
public partial class TankLevelProps : ObservableObject
{
    [ObservableProperty]
    private double _minValue = 0;

    [ObservableProperty]
    private double _maxValue = 100;

    [ObservableProperty]
    private string _unit = "%";

    [ObservableProperty]
    private double? _highAlarm;

    [ObservableProperty]
    private double? _lowAlarm;

    [ObservableProperty]
    private string _colorHex = "#0284C7";

    [ObservableProperty]
    private double _normalizedProgress = 0.0;
}

/// <summary>
/// 科技数显卡片专属组合属性类（包含工程单位、小数位及数码发光色）
/// </summary>
public partial class NumericCardProps : ObservableObject
{
    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private int _decimals = 1;

    [ObservableProperty]
    private string _colorHex = "#0284C7";
}

/// <summary>
/// 多路数字量 IO 状态点阵板专属组合属性类（包含通道路数、显示进制及点亮/底色）
/// </summary>
public partial class IoMatrixProps : ObservableObject
{
    [ObservableProperty]
    private int _ioChannels = 8; // 8 或 16

    [ObservableProperty]
    private string _displayFormat = "HEX"; // HEX, BIN, DEC

    [ObservableProperty]
    private string _activeColor = "#10B981";

    [ObservableProperty]
    private string _inactiveColor = "#334155";
}

/// <summary>
/// 工业高亮状态指示灯专属组合属性类（包含亮灭颜色、双态文案、呼吸闪烁及激活态）
/// </summary>
public partial class StatusLedProps : ObservableObject
{
    [ObservableProperty]
    private string _activeColor = "#10B981";

    [ObservableProperty]
    private string _inactiveColor = "#334155";

    [ObservableProperty]
    private string _onText = "运行中";

    [ObservableProperty]
    private string _offText = "已停机";

    [ObservableProperty]
    private bool _enableBlink;

    [ObservableProperty]
    private bool _isActive;
}

/// <summary>
/// 工业控制按钮专属组合属性类（包含按钮文本、动作模式、下发数值、二次防误触确认）
/// </summary>
public partial class ControlButtonProps : ObservableObject
{
    [ObservableProperty]
    private string _buttonText = "触发控制";

    [ObservableProperty]
    private string _writeValue = "1";

    [ObservableProperty]
    private string _buttonMode = "DirectWrite"; // DirectWrite, Toggle, Momentary

    [ObservableProperty]
    private string _colorHex = "#0284C7";

    [ObservableProperty]
    private bool _requireConfirm;

    [ObservableProperty]
    private string _confirmMessage = "确定要下发此项控制操作吗？";
}
