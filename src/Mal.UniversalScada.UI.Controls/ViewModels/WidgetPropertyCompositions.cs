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
    private string _colorHex = "#10B981";

    [ObservableProperty]
    private double? _lowValue;

    [ObservableProperty]
    private double? _midValue;

    [ObservableProperty]
    private double? _highValue;

    [ObservableProperty]
    private string _lowColor = "#38BDF8";

    [ObservableProperty]
    private string _midColor = "#10B981";

    [ObservableProperty]
    private string _highColor = "#EF4444";

    [ObservableProperty]
    private bool _enableThresholdColor = true;

    [ObservableProperty]
    private string _lowArcData = string.Empty;

    [ObservableProperty]
    private string _midArcData = string.Empty;

    [ObservableProperty]
    private string _highArcData = string.Empty;

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
    private string _orientation = "Vertical"; // "Vertical" (立式/竖向), "Horizontal" (卧式/横向)

    [ObservableProperty]
    private double? _lowLevel;

    [ObservableProperty]
    private double? _midLevel;

    [ObservableProperty]
    private double? _highLevel;

    [ObservableProperty]
    private string _lowColor = "#EAB308";

    [ObservableProperty]
    private string _midColor = "#0284C7";

    [ObservableProperty]
    private string _highColor = "#EF4444";

    [ObservableProperty]
    private string _liquidColor = "#0284C7";

    [ObservableProperty]
    private string _colorHex = "#0284C7";

    [ObservableProperty]
    private double _normalizedProgress = 0.0;

    partial void OnOrientationChanged(string value)
    {
        OnPropertyChanged(nameof(IsHorizontal));
    }

    public bool IsHorizontal => string.Equals(Orientation, "Horizontal", StringComparison.OrdinalIgnoreCase);
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
/// 普通按钮专属组合属性类（包含按钮文本、动作模式、下发数值、二次防误触确认）
/// </summary>
public partial class ControlButtonProps : ObservableObject
{
    [ObservableProperty]
    private string _buttonText = "按钮";

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

/// <summary>
/// 工业工艺管道专属组合属性类 (支持横向/纵向介质流向、流动跑马灯速度、管径与介质颜色)
/// </summary>
public partial class PipeProps : ObservableObject
{
    [ObservableProperty]
    private string _orientation = "Horizontal"; // Horizontal, Vertical

    [ObservableProperty]
    private double _pipeDiameter = 20;

    [ObservableProperty]
    private string _pipeColor = "#1E293B";

    [ObservableProperty]
    private string _liquidColor = "#0284C7";

    [ObservableProperty]
    private string _flowDirection = "Forward"; // Forward, Reverse

    [ObservableProperty]
    private double _flowSpeed = 2.0; // 动画周期秒数

    [ObservableProperty]
    private bool _isFlowing = true;
}

/// <summary>
/// 工业设备状态监视卡片专属组合属性类 (绑定具体下位机设备，展示在线/离线、通信延迟、协议与通道拓扑)
/// </summary>
public partial class DeviceStatusProps : ObservableObject
{
    [ObservableProperty]
    private string _targetDeviceId = string.Empty;

    [ObservableProperty]
    private string _deviceName = "未关联设备";

    [ObservableProperty]
    private string _channelId = "--";

    [ObservableProperty]
    private string _protocol = "Modbus TCP";

    [ObservableProperty]
    private int _stationAddress = 1;

    [ObservableProperty]
    private int _pollIntervalMs = 100;

    [ObservableProperty]
    private string _connectionStatus = "Online"; // Online, Offline, Timeout, Fault

    [ObservableProperty]
    private int _latencyMs = 12;

    [ObservableProperty]
    private int _tagCount = 0;

    [ObservableProperty]
    private string _activeColor = "#10B981";

    [ObservableProperty]
    private string _offlineColor = "#EF4444";

    [ObservableProperty]
    private string _warningColor = "#F59E0B";

    [ObservableProperty]
    private bool _isCompact = false;

    // 友好兼容别名 (方便 XAML 与测试绑定)
    public string ProtocolType
    {
        get => Protocol;
        set => Protocol = value;
    }

    public int ResponseLatencyMs
    {
        get => LatencyMs;
        set => LatencyMs = value;
    }
}



