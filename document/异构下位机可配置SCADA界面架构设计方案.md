# 异构下位机可配置 SCADA/HMI 界面架构设计方案

## 1. 核心痛点与设计目标

在工业自动化现场，下位机（PLC、智能仪表、单片机/嵌入式板卡、专用运动控制器）种类繁多，不同工艺设备的界面展现诉求存在巨大差异：
- **数控机床 / 多轴运动平台**：关注坐标轴实时位置（X/Y/Z/A/B）、进给速度、主轴转速、加工零件计数、G 代码行号、限位与报警状态；
- **温控箱 / 烘道 / 反应釜**：关注 PV 当前测温曲线、SV 设定温度、升温速率（℃/min）、PID 输出百分比（0~100%）、多温区均匀度同屏比对；
- **配料 / 灌装 / 水处理流体系统**：关注储罐动态液位高度（百分比与升降填充动画）、管道介质流向跑马灯、气动/电动阀门开闭指示、泵启停与变频转速；
- **产线测试台 / 多通道巡检仪 / 单片机嵌入式设备**：关注多路电压/电流/功率采样表格、Pass/Fail 巨幅合格判定、扫码枪条码追溯输入；
- **通用 PLC 产线工位**：关注 DI/DO 状态指示灯点阵、安全门锁/光栅安全状态、节拍计时、急停与故障复位。

**核心设计目标**：
1. **关注点分离 (Separation of Concerns)**：底层物理驱动（Modbus TCP/RTU/ASCII、西门子 S7、三菱 MC、串口私有协议）仅负责寄存器周期采集与指令下发，输出标准点位快照 (`TagValueSnapshot`)；UI 层通过**数据绑定与元数据声明**实现彻底解耦，现场更换下位机、增加传感器或调整工艺时，**无需修改一行 C# 代码，无需重新编译发布系统**。
2. **渐进式三层演进模型**：
   - **基础层：元数据驱动的响应式卡片看板 (Responsive Widget Dashboard)** —— 投入产出比最高，通过组态勾选配置，零代码秒级生成现代化、科技感十足的设备卡片看板。
   - **进阶层：设备物模型与预设模板体系 (Device Digital Twin & Templates)** —— 面向标准机型（如温控器、变频器、IO 站），提供一键套用模板。
   - **高级层：工业组态自由工艺画布 (P&ID 2D Canvas Designer)** —— 提供自由拖拽图元、管道流动动画、现场工艺拓扑仿真。

---

## 2. 界面可配置化总体架构

```mermaid
flowchart TD
    subgraph DataLayer [1. 底层数据与驱动层 (Core & Drivers)]
        Driver[IDriver: S7 / Modbus / CustomSerial / MC] --> Channel[IChannel: TCP / Serial]
        Driver --> TagHub[TagRuntimeHub: 内存实时点位池与高频发布]
    end

    subgraph ConfigLayer [2. 界面组态与配置仓储 (Storage.Sqlite)]
        ViewConfig[视图配置 (ViewDashboardConfig): 页面名称/关联设备/布局模式]
        WidgetConfig[控件配置 (WidgetConfig): 控件类型/标题/网格坐标/属性字典]
        BindingConfig[点位绑定: 主点位 PrimaryTag / 辅助点位 AuxTags / 交互 Action]
    end

    subgraph UIEngine [3. WPF 动态渲染引擎 (UI.Wpf 运行态)]
        LayoutManager[DynamicLayoutManager 布局排版管理器]
        TemplateSelector[WidgetTemplateSelector 数据模板路由选择器]
        WidgetRegistry[工控可视化组件库注册中心]
        
        LayoutManager --> TemplateSelector
        TemplateSelector --> WidgetRegistry
    end

    subgraph ComponentLibrary [4. 标准化工控可视化组件库 (Widgets)]
        W1[IO 指示灯矩阵 / 继电器状态板]
        W2[工业 270° 圆形仪表盘 / 柱状标尺]
        W3[立式/卧式动态液体储罐 / 阀门]
        W4[实时时序趋势曲线 (LiveCharts2 / Micro-Sparkline)]
        W5[多通道数据监控网格 / 极值统计卡片]
        W6[设定值输入框 / 旋钮 / 启停控制按钮]
        W7[实时报警流水横幅]
    end

    TagHub -->|实时点位推送| UIEngine
    ViewConfig --> UIEngine
    WidgetConfig --> UIEngine
    BindingConfig --> UIEngine
    WidgetRegistry --> ComponentLibrary
```

---

## 3. 核心数据模型设计 (UI Configuration Models)

### 3.1 工业控件类型枚举 (`WidgetType`)

```csharp
namespace Mal.UniversalScada.Core.Models;

/// <summary>
/// SCADA 工业可视化控件类型
/// </summary>
public enum WidgetType
{
    // 1. 状态与指示类
    StatusLed = 1,          // 单点/双色/三色高亮指示灯 (运行绿/停止灰/故障红闪烁)
    IoMatrix = 2,           // 多路数字量 DI/DO 点阵状态板 (8/16/32 路，支持点击强制置位)
    PassFailBanner = 3,     // 生产测试合格/不合格巨幅状态指示卡片 (PASS 绿底 / FAIL 红底)
    TowerLight = 4,         // 三色警示塔灯 (红/黄/绿/蜂鸣器动态模拟)

    // 2. 模拟量显示与仪表类
    GaugeCircular = 10,     // 270°/360° 工业环形仪表盘 (带量程范围与红黄绿告警分段)
    LevelTank = 11,         // 立式/卧式储罐 (带真实液体波动起伏与百分比高度升降动画)
    BarLinear = 12,         // 垂直/水平柱状标尺刻度
    NumericCard = 13,       // 工业数码大字展示卡片 (数值 + 趋势小箭头 + 单位 + 极值)

    // 3. 曲线与趋势类
    RealtimeTrend = 20,     // 实时时序折线走势图 (多通道同屏对比，支持标尺取词与暂停排查)
    SpectrumHistogram = 21, // 频段分布柱状图

    // 4. 控制与交互类
    MomentaryButton = 30,   // 点动控制按钮 (按住下发置 1，松开下发清 0，专用于设备 JOG 点动)
    ToggleButton = 31,      // 保持自锁切换开关 (Toggle Switch / 旋转旋钮)
    SetpointInput = 32,     // 目标设定值输入框 (带上下限防呆限值校验与二次确认弹窗)

    // 5. 综合工艺复合类
    PidController = 40,     // 温控/PID 专用仪表卡 (PV 测量值、SV 设定值、MV 输出功率百分比)
    AxisMotion = 41,        // 单轴/多轴运动控制卡 (当前坐标、目标坐标、点动、回零、正负限位)
    
    // 6. 开放扩展
    CustomXElement = 99     // 自定义 XAML / 插件扩展容器
}
```

### 3.2 组件配置模型 (`WidgetConfig`)

```csharp
namespace Mal.UniversalScada.Core.Models;

/// <summary>
/// 单个可视化卡片组件的组态元数据
/// </summary>
public class WidgetConfig
{
    /// <summary>
    /// 组件全局唯一 ID
    /// </summary>
    public string WidgetId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 卡片标题 (如 "1号主轴转速", "A温区实时温度", "储液罐液位")
    /// </summary>
    public string Title { get; set; } = "监控项";

    /// <summary>
    /// 控件表现形式分类
    /// </summary>
    public WidgetType Type { get; set; } = WidgetType.NumericCard;

    /// <summary>
    /// 绑定的主采集点位 ID (如 "PLC_Main.Spindle_Speed")
    /// </summary>
    public string PrimaryTagId { get; set; } = string.Empty;

    /// <summary>
    /// 辅助绑定点位字典 (支持复合控件，如温控卡片包含 PV、SV、加热状态三个点位)
    /// Key: 点位角色 (如 "PV", "SV", "OutputPower", "FaultAlarm")
    /// Value: 实际绑定的 TagId
    /// </summary>
    public Dictionary<string, string> AuxiliaryTags { get; set; } = new();

    /// <summary>
    /// 网格布局参数 (自适应网格中的行、列及跨度)
    /// </summary>
    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;

    /// <summary>
    /// 绝对尺寸 (默认 0 表示按布局器自适应拉伸)
    /// </summary>
    public double Width { get; set; } = 0;
    public double Height { get; set; } = 0;

    /// <summary>
    /// 控件自定义视觉与业务属性字典 (JSON 序列化持久化)
    /// 例如:
    /// ["Min"] = "0", ["Max"] = "3000", ["Unit"] = "rpm",
    /// ["HighAlarm"] = "2800", ["LowAlarm"] = "200",
    /// ["ThemeColor"] = "#00E5FF", ["TankShape"] = "Cylinder"
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();

    /// <summary>
    /// 下发交互动作配置 (如点击按钮后下发的具体值或触发的操作)
    /// </summary>
    public WidgetActionConfig? Action { get; set; }
}

/// <summary>
/// 控件交互触发行为配置
/// </summary>
public class WidgetActionConfig
{
    /// <summary>
    /// 交互类型 (直接下发写入 / 弹出确认对话框 / 切换画面 / 执行配方)
    /// </summary>
    public string ActionType { get; set; } = "DirectWrite";

    /// <summary>
    /// 下发目标点位 ID (若为空则默认使用 PrimaryTagId)
    /// </summary>
    public string? TargetTagId { get; set; }

    /// <summary>
    /// 默认下发目标值 (例如 bool 类型的 true，或复位指令码)
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 操作确认提示语 (防止误触重大控制，例如 "确定要启动1号主搅拌电机吗？")
    /// </summary>
    public string? ConfirmPrompt { get; set; }
}
```

### 3.3 页面视图配置模型 (`ViewDashboardConfig`)

```csharp
namespace Mal.UniversalScada.Core.Models;

/// <summary>
/// 监控视图/工艺画面组态配置
/// </summary>
public class ViewDashboardConfig
{
    /// <summary>
    /// 视图唯一标识 ID (如 "View_Station_1", "View_Heating_Oven")
    /// </summary>
    public string ViewId { get; set; } = "View_Default";

    /// <summary>
    /// 视图名称 (如 "1号灌装主工位", "回流焊多温区监控")
    /// </summary>
    public string Name { get; set; } = "主监控画面";

    /// <summary>
    /// 菜单图标名称 (MaterialDesign / Segoe 图标名)
    /// </summary>
    public string Icon { get; set; } = "ViewDashboard";

    /// <summary>
    /// 所关联监控的下位机设备 ID 列表 (支持单视图聚合多个异构设备)
    /// </summary>
    public List<string> BoundDeviceIds { get; set; } = new();

    /// <summary>
    /// 画面排版布局模式
    /// - ResponsiveGrid: 现代响应式自适应卡片流 (首选推荐)
    /// - FreeCanvas: 绝对像素坐标工艺流程画布
    /// </summary>
    public string LayoutMode { get; set; } = "ResponsiveGrid";

    /// <summary>
    /// 背景图绝对路径或资源名称 (仅用于 FreeCanvas 工艺底图)
    /// </summary>
    public string? BackgroundImagePath { get; set; }

    /// <summary>
    /// 画面内包含的所有卡片组件列表
    /// </summary>
    public List<WidgetConfig> Widgets { get; set; } = new();
}
```

---

## 4. 解决“不同下位机展示差异”的三种实战落地策略

### 策略 1：卡片式自适应看板（投入产出比最高，强烈推荐首选）

- **技术实现**：
  - 左侧为【设备导航树 / 产线工位目录】；
  - 右侧主区域使用 WPF `ItemsControl`，其 `ItemsPanel` 配置为自适应网格或流式卡片容器 (`WrapPanel` / 自定义 `StaggeredGrid`)；
  - 核心利用 WPF 的 **`ItemTemplateSelector`**：根据 `WidgetConfig.Type` 动态挑选对应的 `DataTemplate`（如仪表盘模板、IO 点阵模板、曲线模板）。
- **业务优势**：
  - 零代码开发新下位机界面：现场工程师只需在组态前台勾选点位并指定控件类型，界面瞬间自动生成；
  - 统一视觉美学：全暗色系工业微光风格（Deep Navy + Cyan/Emerald/Amber 霓虹状态），第一眼极具科技感与高级感；
  - 自适应屏幕分辨率：工控机 1080P、工位小屏 720P 或大屏 4K 均能自适应重排，不发生挤压或变形。

### 策略 2：设备物模型与预设模板库（面向批量同类机型）

- **技术实现**：
  - 在系统中固化针对典型工业设备的**开箱即用物模型模板 (Device Templates)**：
    1. **温控表模板 (Temperature Controller Template)**：
       - 左侧：巨幅 PV 测量温、SV 设定温、升温速率指示；
       - 中间：温度实时时序趋势折线；
       - 右侧：输出功率指示条与 PID 参数微调面板。
    2. **变频器 / 伺服运动模板 (Inverter & Motion Template)**：
       - 顶部：运行/就绪/报警状态塔灯；
       - 中间：大刻度转速仪表盘 + 电流/母线电压；
       - 底部：正转/反转/停止控制按钮与频率设定滑块。
    3. **分布式远程 IO 模块模板 (Remote IO Matrix Template)**：
       - 8/16/32 位紧凑指示灯矩阵，清晰显示每个物理通道的高低电平，悬浮查看点位名称，点击可强制输出。
    4. **单片机多路仪器仪表测试模板 (MCU Multichannel Tester Template)**：
       - 多通道采样数据表格实时滚动，带 Pass/Fail 大字提示和蜂鸣器警报联动。
- **业务优势**：
  - 新增一台同型号温控器或变频器时，工程师只需在界面上点击【套用温控器模板】，将实际点位拖入模板的预置插槽（PV、SV、Alarm），无需逐个布局，10 秒内完成配置上线。

### 策略 3：2D 工艺流程仿真画布 (P&ID Mimic Display，终极组态形态)

- **技术实现**：
  - 允许导入车间 CAD / 工艺流程图（PNG/SVG 底图）；
  - 画布上放置带有动态属性绑定的工业矢量图元（电机、管道、储罐、截止阀、气缸）：
    - 管道：根据泵启停点位，控制流动虚线动画的开启与流向；
    - 电机：根据运行点位，控制风扇旋转动画；
    - 储罐：根据液位点位，控制内部液面高度与高低警戒线变色。
- **业务优势**：
  - 极其直观地还原工厂物理空间与生产流程，是面向厂长与客户验收演示最具视觉冲击力的展现形式。

---

## 5. 工程实施路线图 (Roadmap)

### 第一阶段：核心工控组件库构建 (`Mal.UniversalScada.UI.Wpf`)
1. 封装 6 种工业基础可视化控件：
   - `LedIndicator.xaml` (数字量高亮指示灯，支持常亮、灰暗、红光呼吸闪烁)
   - `IndustrialGauge.xaml` (模拟量 270° 弧形仪表盘，带安全/警告/危险三色渐变光带)
   - `TankLevelControl.xaml` (动态液体储罐，支持立式圆柱、卧式椭圆与波浪填充)
   - `NumericValueCard.xaml` (大字数据展示卡片，带单位标签与微型趋势走势线 Sparkline)
   - `IoMatrixPanel.xaml` (16/32 路紧凑型 DI/DO 点阵板)
   - `IndustrialControlButton.xaml` (工业带灯自锁与点动控制按钮)

### 第二阶段：动态卡片流看板引擎 (`DynamicDashboardView`)
1. 实现 `WidgetTemplateSelector` 与动态网格排版系统；
2. 接入 Core 的实时点位更新总线，利用 MVVM 数据绑定实现 **50ms 刷新率下的 60FPS 极低 CPU 占用率流畅渲染**。

### 第三阶段：组态前台可视化设计器联动 (`Configurator.Wpf`)
1. 在组态软件中增加【界面布局设计器】模块；
2. 支持树形点位拖拽到画布、右侧属性面板配置量程与单位、卡片预览与保存。
