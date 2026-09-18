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

/// <summary>
/// 文本标签专属组合属性类 (支持静态文本/动态点位展示、字号、颜色与对齐)
/// </summary>
public partial class TextLabelProps : ObservableObject
{
    [ObservableProperty]
    private string _text = "文本标签 / 工位说明";

    [ObservableProperty]
    private double _fontSize = 14;

    [ObservableProperty]
    private string _textColor = "#F8FAFC";

    [ObservableProperty]
    private bool _isBold = true;

    [ObservableProperty]
    private string _alignment = "Left"; // Left, Center, Right
}

/// <summary>
/// 普通显示框专属组合属性类 (紧凑型标准工控数显/文本框，带前缀标签、实时值、单位及边框底色)
/// </summary>
public partial class DisplayBoxProps : ObservableObject
{
    [ObservableProperty]
    private string _prefix = "实时量测";

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private int _decimals = 2;

    [ObservableProperty]
    private string _textColor = "#38BDF8";

    [ObservableProperty]
    private string _borderColor = "#334155";

    [ObservableProperty]
    private string _backgroundColor = "#0F172A";

    [ObservableProperty]
    private string _alignment = "Right"; // Left, Center, Right
}

/// <summary>
/// 实时趋势折线图专属组合属性类 (模拟量波形波动监控、时间网格与曲线颜色)
/// </summary>
public partial class TrendChartProps : ObservableObject
{
    [ObservableProperty]
    private double _minValue = 0;

    [ObservableProperty]
    private double _maxValue = 100;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private string _lineColor = "#38BDF8";

    [ObservableProperty]
    private string _fillColor = "#0369A1";

    [ObservableProperty]
    private string _gridColor = "#1E293B";

    [ObservableProperty]
    private int _timeWindowSeconds = 60;
}

/// <summary>
/// 区域容器分组框专属组合属性类 (分组标题、边框、填充底色与圆角)
/// </summary>
public partial class PanelContainerProps : ObservableObject
{
    [ObservableProperty]
    private string _groupTitle = "工位区域分组";

    [ObservableProperty]
    private string _headerBgColor = "#1E293B";

    [ObservableProperty]
    private string _borderColor = "#38BDF8";

    [ObservableProperty]
    private string _fillColor = "#0A0F1D";

    [ObservableProperty]
    private double _cornerRadius = 8;

    [ObservableProperty]
    private double _borderThickness = 1;
}

