using System.Text.Json.Serialization;

namespace Mal.UniversalScada.Core.Models;

/// <summary>
/// SCADA 工业可视化控件类型
/// </summary>
public enum WidgetType
{
    /// <summary>
    /// 270° 工业圆形仪表盘 (模拟量展示，带量程与安全/告警分段)
    /// </summary>
    GaugeCircular = 1,

    /// <summary>
    /// 动态液体储罐 (立式/卧式，带液位高度百分比与液体填充动画)
    /// </summary>
    LevelTank = 2,

    /// <summary>
    /// 工业数码大字科技卡片 (点位名称、大号数值、单位徽章、通信品质状态)
    /// </summary>
    NumericCard = 3,

    /// <summary>
    /// 多路数字量 DI/DO 点阵状态板 (8/16 路状态灯矩阵，支持交互置位)
    /// </summary>
    IoMatrix = 4,

    /// <summary>
    /// 工业高亮状态指示灯 (单点布尔量，支持绿/红/灰及呼吸闪烁)
    /// </summary>
    StatusLed = 5,

    /// <summary>
    /// 控制按钮 (点动按压置 1 或自锁切换开关，用于下发启停/复位控制指令)
    /// </summary>
    ControlButton = 6,

    /// <summary>
    /// 设定值输入框 (参数设定、目标值下发，带防呆校验)
    /// </summary>
    SetpointInput = 7,

    /// <summary>
    /// 静态/动态文本标签 (设备铭牌、区域指示、工位说明)
    /// </summary>
    TextLabel = 8,

    /// <summary>
    /// 普通显示框 (紧凑型标准单行工控文本/数值显示框)
    /// </summary>
    DisplayBox = 9,

    /// <summary>
    /// 实时趋势折线图 (模拟量实时波动曲线监控)
    /// </summary>
    TrendChart = 10,

    /// <summary>
    /// 区域容器分组框 (工位边框、组件容器卡片与背景框)
    /// </summary>
    PanelContainer = 11,

    /// <summary>
    /// P&amp;ID 工业工艺管道图元 (带介质颜色与跑马灯流动动效)
    /// </summary>
    Pipe = 12
}

/// <summary>
/// 控件交互触发行为配置
/// </summary>
public class WidgetActionConfig
{
    /// <summary>
    /// 交互类型 (DirectWrite 直接写入 / Toggle 翻转写入 / Setpoint 设定值输入)
    /// </summary>
    public string ActionType { get; set; } = "DirectWrite";

    /// <summary>
    /// 目标写入点位 ID (若为空则默认使用 PrimaryTagId)
    /// </summary>
    public string? TargetTagId { get; set; }

    /// <summary>
    /// 写入的目标值
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 操作前确认提示语 (为空时不弹窗确认)
    /// </summary>
    public string? ConfirmPrompt { get; set; }
}

/// <summary>
/// 所见即所得画布上的单个可视化卡片配置元数据
/// </summary>
public class WidgetConfig
{
    /// <summary>
    /// 控件全局唯一 ID
    /// </summary>
    public string WidgetId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 控件标题 / 友好名称 (如 "1号主轴转速", "反应釜液位", "急停状态")
    /// </summary>
    public string Title { get; set; } = "监控项";

    /// <summary>
    /// 控件类型
    /// </summary>
    public WidgetType Type { get; set; } = WidgetType.NumericCard;

    /// <summary>
    /// 画布绝对横坐标 X (像素)
    /// </summary>
    public double X { get; set; } = 50;

    /// <summary>
    /// 画布绝对纵坐标 Y (像素)
    /// </summary>
    public double Y { get; set; } = 50;

    /// <summary>
    /// 控件宽度 Width (像素)
    /// </summary>
    public double Width { get; set; } = 240;

    /// <summary>
    /// 控件高度 Height (像素)
    /// </summary>
    public double Height { get; set; } = 180;

    /// <summary>
    /// 绑定的主采集点位 ID (对应 TagNode.TagId)
    /// </summary>
    public string PrimaryTagId { get; set; } = string.Empty;

    /// <summary>
    /// 辅助绑定点位字典 (支持复合控件，如温控卡片包含 PV、SV、加热状态)
    /// </summary>
    public Dictionary<string, string> AuxiliaryTags { get; set; } = new();

    /// <summary>
    /// 控件扩展视觉与工程属性字典 (如 Min="0", Max="3000", Unit="rpm", HighAlarm="2800")
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();

    /// <summary>
    /// 控制下发交互行为
    /// </summary>
    public WidgetActionConfig? Action { get; set; }

    /// <summary>
    /// 便捷提取属性辅助方法
    /// </summary>
    public string GetProp(string key, string defaultVal = "")
    {
        return Properties.TryGetValue(key, out var v) ? v : defaultVal;
    }

    public double GetDoubleProp(string key, double defaultVal = 0.0)
    {
        if (Properties.TryGetValue(key, out var v) && double.TryParse(v, out var d))
        {
            return d;
        }
        return defaultVal;
    }

    public void SetProp(string key, object val)
    {
        Properties[key] = val?.ToString() ?? string.Empty;
    }
}

/// <summary>
/// 一套完整的下位机监控 UI 画面配置
/// </summary>
public class UiViewConfig
{
    /// <summary>
    /// 画面唯一标识 ID (如 "View_Oven_1", "View_Filling_Station")
    /// </summary>
    public string ViewId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 画面展示名称 (如 "1号回流焊多温区看板", "2号自动灌装主控台")
    /// </summary>
    public string Name { get; set; } = "新建监控画面";

    /// <summary>
    /// 关联的设备 ID (支持空或关联特定下位机设备)
    /// </summary>
    public string? BoundDeviceId { get; set; }

    /// <summary>
    /// 画布排版模式 (Canvas 自由画布 / ResponsiveGrid 响应式流)
    /// </summary>
    public string LayoutMode { get; set; } = "Canvas";

    /// <summary>
    /// 画布参考宽度 (默认 1920)
    /// </summary>
    public double CanvasWidth { get; set; } = 1920;

    /// <summary>
    /// 画布参考高度 (默认 1080)
    /// </summary>
    public double CanvasHeight { get; set; } = 1080;

    /// <summary>
    /// 是否为启动默认加载展示的画面
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// 画布背景色 (如 "#0F172A", "Transparent")
    /// </summary>
    public string BackgroundColor { get; set; } = "#0F172A";

    /// <summary>
    /// 画布背景图片路径 (本地文件绝对路径或相对路径)
    /// </summary>
    public string? BackgroundImagePath { get; set; }

    /// <summary>
    /// 背景图片拉伸方式 (Uniform / UniformToFill / Fill / None)
    /// </summary>
    public string BackgroundImageStretch { get; set; } = "UniformToFill";

    /// <summary>
    /// 背景图片透明度 (0.0 ~ 1.0)
    /// </summary>
    public double BackgroundImageOpacity { get; set; } = 0.85;

    /// <summary>
    /// 画面上布局的所有可视化卡片列表
    /// </summary>
    public List<WidgetConfig> Widgets { get; set; } = new();

    /// <summary>
    /// 最近更新时间
    /// </summary>
    public DateTime UpdatedTime { get; set; } = DateTime.Now;
}
