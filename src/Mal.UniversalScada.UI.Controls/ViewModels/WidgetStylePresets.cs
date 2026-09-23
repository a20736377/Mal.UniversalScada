using System;
using System.Collections.Generic;
using System.Linq;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.UI.Controls.ViewModels;

/// <summary>
/// 组件样式与行业预设定义类
/// </summary>
public record WidgetStylePreset(
    string Id,
    WidgetType Type,
    string Name,
    string Icon,
    string Category,
    string Description,
    Action<WidgetViewModel> Apply
);

/// <summary>
/// 全局样式预设注册目录 (支持各工业组件秒级一键样式切换)
/// </summary>
public static class WidgetStylePresetCatalog
{
    private static readonly Dictionary<WidgetType, List<WidgetStylePreset>> _presets = new();

    static WidgetStylePresetCatalog()
    {
        RegisterCircularGaugePresets();
        RegisterArcGaugePresets();
        RegisterControlButtonPresets();
        RegisterTankLevelPresets();
        RegisterStatusLedPresets();
        RegisterNumericCardPresets();
        RegisterTrendChartPresets();
        RegisterIoMatrixPresets();
        RegisterTextLabelPresets();
        RegisterDisplayBoxPresets();
        RegisterPanelContainerPresets();
        RegisterPipePresets();
        RegisterDeviceStatusPresets();
    }

    public static IReadOnlyList<WidgetStylePreset> GetPresets(WidgetType type)
    {
        if (_presets.TryGetValue(type, out var list))
        {
            return list;
        }
        return Array.Empty<WidgetStylePreset>();
    }

    private static void Register(WidgetStylePreset preset)
    {
        if (!_presets.TryGetValue(preset.Type, out var list))
        {
            list = new List<WidgetStylePreset>();
            _presets[preset.Type] = list;
        }
        list.Add(preset);
    }

    #region 270° 圆形仪表盘预设 (CircularGauge)
    private static void RegisterCircularGaugePresets()
    {
        Register(new WidgetStylePreset(
            Id: "Gauge_AC_Voltage_380",
            Type: WidgetType.GaugeCircular,
            Name: "交流电压表 (0~500V)",
            Icon: "⚡",
            Category: "电气电力",
            Description: "标准三相动力线电压监测，340V欠压，420V过压报警",
            Apply: vm =>
            {
                if (vm is CircularGaugeWidgetViewModel g)
                {
                    g.Title = "交流动力电压";
                    g.Unit = "V";
                    g.MinValue = 0;
                    g.MaxValue = 500;
                    g.Props.LowValue = 340;
                    g.Props.MidValue = 380;
                    g.Props.HighValue = 420;
                    g.LowAlarm = 340;
                    g.HighAlarm = 420;
                    g.Props.LowColor = "#F59E0B";  // 欠压黄
                    g.Props.MidColor = "#10B981";  // 正常绿
                    g.Props.HighColor = "#EF4444"; // 过压红
                    g.Props.EnableThresholdColor = true;
                    g.Decimals = 1;
                    g.UpdateSegmentArcs();
                    g.ReevaluateGaugeColor();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Gauge_AC_Voltage_220",
            Type: WidgetType.GaugeCircular,
            Name: "单相市电电压表 (0~300V)",
            Icon: "🔌",
            Category: "电气电力",
            Description: "220V单相供电监视，198V~242V合格稳压绿区",
            Apply: vm =>
            {
                if (vm is CircularGaugeWidgetViewModel g)
                {
                    g.Title = "单相市电电压";
                    g.Unit = "V";
                    g.MinValue = 0;
                    g.MaxValue = 300;
                    g.Props.LowValue = 198;
                    g.Props.MidValue = 220;
                    g.Props.HighValue = 242;
                    g.LowAlarm = 198;
                    g.HighAlarm = 242;
                    g.Props.LowColor = "#F59E0B";
                    g.Props.MidColor = "#10B981";
                    g.Props.HighColor = "#EF4444";
                    g.Props.EnableThresholdColor = true;
                    g.Decimals = 1;
                    g.UpdateSegmentArcs();
                    g.ReevaluateGaugeColor();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Gauge_Motor_Current_100",
            Type: WidgetType.GaugeCircular,
            Name: "电机负载电流表 (0~100A)",
            Icon: "🔋",
            Category: "电气电力",
            Description: "电机工作电流监控，额定80A，超85A过载红区报警",
            Apply: vm =>
            {
                if (vm is CircularGaugeWidgetViewModel g)
                {
                    g.Title = "主电机工作电流";
                    g.Unit = "A";
                    g.MinValue = 0;
                    g.MaxValue = 100;
                    g.Props.LowValue = 10;
                    g.Props.MidValue = 50;
                    g.Props.HighValue = 85;
                    g.LowAlarm = null;
                    g.HighAlarm = 85;
                    g.Props.LowColor = "#38BDF8";  // 浅载蓝
                    g.Props.MidColor = "#10B981";  // 额定绿
                    g.Props.HighColor = "#EF4444"; // 过载红
                    g.Props.EnableThresholdColor = true;
                    g.Decimals = 1;
                    g.UpdateSegmentArcs();
                    g.ReevaluateGaugeColor();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Gauge_Main_Current_500",
            Type: WidgetType.GaugeCircular,
            Name: "进线总电流表 (0~500A)",
            Icon: "⚡",
            Category: "电气电力",
            Description: "配电主柜总进线电流监视，超420A高限报警",
            Apply: vm =>
            {
                if (vm is CircularGaugeWidgetViewModel g)
                {
                    g.Title = "进线总回路电流";
                    g.Unit = "A";
                    g.MinValue = 0;
                    g.MaxValue = 500;
                    g.Props.LowValue = 50;
                    g.Props.MidValue = 280;
                    g.Props.HighValue = 420;
                    g.LowAlarm = null;
                    g.HighAlarm = 420;
                    g.Props.LowColor = "#38BDF8";
                    g.Props.MidColor = "#10B981";
                    g.Props.HighColor = "#EF4444";
                    g.Props.EnableThresholdColor = true;
                    g.Decimals = 0;
                    g.UpdateSegmentArcs();
                    g.ReevaluateGaugeColor();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Gauge_Oven_Temp_300",
            Type: WidgetType.GaugeCircular,
            Name: "热工炉膛温度表 (0~300℃)",
            Icon: "🔥",
            Category: "热工温度",
            Description: "回流焊/烘箱/加热炉膛测温，80℃预热，250℃超温保护",
            Apply: vm =>
            {
                if (vm is CircularGaugeWidgetViewModel g)
                {
                    g.Title = "炉膛恒温区温度";
                    g.Unit = "℃";
                    g.MinValue = 0;
                    g.MaxValue = 300;
                    g.Props.LowValue = 80;
                    g.Props.MidValue = 180;
                    g.Props.HighValue = 250;
                    g.LowAlarm = 80;
                    g.HighAlarm = 250;
                    g.Props.LowColor = "#38BDF8";
                    g.Props.MidColor = "#10B981";
                    g.Props.HighColor = "#EF4444";
                    g.Props.EnableThresholdColor = true;
                    g.Decimals = 1;
                    g.UpdateSegmentArcs();
                    g.ReevaluateGaugeColor();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Gauge_Ambient_Temp",
            Type: WidgetType.GaugeCircular,
            Name: "环境温湿度表 (-20~80℃)",
            Icon: "🌡️",
            Category: "热工温度",
            Description: "无尘车间/机房环境温度监测，0~40℃舒适适宜区间",
            Apply: vm =>
            {
                if (vm is CircularGaugeWidgetViewModel g)
                {
                    g.Title = "车间机房环境温度";
                    g.Unit = "℃";
                    g.MinValue = -20;
                    g.MaxValue = 80;
                    g.Props.LowValue = 5;
                    g.Props.MidValue = 25;
                    g.Props.HighValue = 40;
                    g.LowAlarm = 5;
                    g.HighAlarm = 40;
                    g.Props.LowColor = "#38BDF8";
                    g.Props.MidColor = "#10B981";
                    g.Props.HighColor = "#EF4444";
                    g.Props.EnableThresholdColor = true;
                    g.Decimals = 1;
                    g.UpdateSegmentArcs();
                    g.ReevaluateGaugeColor();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Gauge_Water_Pressure",
            Type: WidgetType.GaugeCircular,
            Name: "管网水气压力表 (0~1.0 MPa)",
            Icon: "🚰",
            Category: "压力流体",
            Description: "循环冷却水/压缩空气总管压力，0.3~0.8 MPa恒压绿区",
            Apply: vm =>
            {
                if (vm is CircularGaugeWidgetViewModel g)
                {
                    g.Title = "管道气液供给压力";
                    g.Unit = "MPa";
                    g.MinValue = 0;
                    g.MaxValue = 1.0;
                    g.Props.LowValue = 0.3;
                    g.Props.MidValue = 0.6;
                    g.Props.HighValue = 0.8;
                    g.LowAlarm = 0.3;
                    g.HighAlarm = 0.8;
                    g.Props.LowColor = "#F59E0B";
                    g.Props.MidColor = "#10B981";
                    g.Props.HighColor = "#EF4444";
                    g.Props.EnableThresholdColor = true;
                    g.Decimals = 2;
                    g.UpdateSegmentArcs();
                    g.ReevaluateGaugeColor();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Gauge_Hydraulic_Pressure",
            Type: WidgetType.GaugeCircular,
            Name: "液压站高压表 (0~25 MPa)",
            Icon: "🚜",
            Category: "压力流体",
            Description: "液压机站工作压力，超21 MPa高压泄载报警",
            Apply: vm =>
            {
                if (vm is CircularGaugeWidgetViewModel g)
                {
                    g.Title = "液压站工作压力";
                    g.Unit = "MPa";
                    g.MinValue = 0;
                    g.MaxValue = 25;
                    g.Props.LowValue = 3;
                    g.Props.MidValue = 15;
                    g.Props.HighValue = 21;
                    g.LowAlarm = null;
                    g.HighAlarm = 21;
                    g.Props.LowColor = "#38BDF8";
                    g.Props.MidColor = "#10B981";
                    g.Props.HighColor = "#EF4444";
                    g.Props.EnableThresholdColor = true;
                    g.Decimals = 1;
                    g.UpdateSegmentArcs();
                    g.ReevaluateGaugeColor();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Gauge_Motor_Speed_3000",
            Type: WidgetType.GaugeCircular,
            Name: "变频电机转速表 (0~3000 rpm)",
            Icon: "🔄",
            Category: "动力机械",
            Description: "主轴驱动电机实时转速，超2850 rpm超速报警",
            Apply: vm =>
            {
                if (vm is CircularGaugeWidgetViewModel g)
                {
                    g.Title = "主轴电机实时转速";
                    g.Unit = "rpm";
                    g.MinValue = 0;
                    g.MaxValue = 3000;
                    g.Props.LowValue = 500;
                    g.Props.MidValue = 1500;
                    g.Props.HighValue = 2850;
                    g.LowAlarm = null;
                    g.HighAlarm = 2850;
                    g.Props.LowColor = "#38BDF8";
                    g.Props.MidColor = "#10B981";
                    g.Props.HighColor = "#EF4444";
                    g.Props.EnableThresholdColor = true;
                    g.Decimals = 0;
                    g.UpdateSegmentArcs();
                    g.ReevaluateGaugeColor();
                }
            }
        ));
    }
    #endregion

    #region 180° 拱形仪表盘预设 (ArcGauge)
    private static void RegisterArcGaugePresets()
    {
        Register(new WidgetStylePreset(
            Id: "ArcGauge_Spindle_Speed_10000",
            Type: WidgetType.GaugeArc,
            Name: "主轴高精双向转速表 (-10000~10000 rpm)",
            Icon: "🧭",
            Category: "高精测控",
            Description: "主轴正反向动态转速监测，±10000 rpm 双极性半圆拱形刻度，霓虹青蓝发光指针",
            Apply: vm =>
            {
                if (vm is ArcGaugeWidgetViewModel g)
                {
                    g.Title = "主轴转速";
                    g.Unit = "rpm";
                    g.MinValue = -10000;
                    g.MaxValue = 10000;
                    g.ColorHex = "#00D2FF";
                    g.Decimals = 1;
                    g.HighAlarm = 9000;
                    g.LowAlarm = -9000;
                    g.UpdateScaleGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "ArcGauge_Micro_Spindle_30000",
            Type: WidgetType.GaugeArc,
            Name: "高速微型电主轴转速表 (-30000~30000 rpm)",
            Icon: "⚡",
            Category: "高精测控",
            Description: "精密雕铣/高速磨头电主轴转速监测，超28000 rpm报警",
            Apply: vm =>
            {
                if (vm is ArcGaugeWidgetViewModel g)
                {
                    g.Title = "高速电主轴转速";
                    g.Unit = "rpm";
                    g.MinValue = -30000;
                    g.MaxValue = 30000;
                    g.ColorHex = "#38BDF8";
                    g.Decimals = 0;
                    g.HighAlarm = 28000;
                    g.LowAlarm = -28000;
                    g.UpdateScaleGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "ArcGauge_Current_RMS_1",
            Type: WidgetType.GaugeArc,
            Name: "双极性交流电流表 (-1.0~1.0 ARMS)",
            Icon: "⚡",
            Category: "电气测控",
            Description: "多通道精密有效值电流监测，±1.0 ARMS 范围，0.1 高精度分辨率",
            Apply: vm =>
            {
                if (vm is ArcGaugeWidgetViewModel g)
                {
                    g.Title = "CH1 电流";
                    g.Unit = "ARMS";
                    g.MinValue = -1.0;
                    g.MaxValue = 1.0;
                    g.ColorHex = "#00D2FF";
                    g.Decimals = 1;
                    g.HighAlarm = 0.9;
                    g.LowAlarm = -0.9;
                    g.UpdateScaleGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "ArcGauge_Current_RMS_5",
            Type: WidgetType.GaugeArc,
            Name: "互感器二次侧相电流表 (-5.0~5.0 ARMS)",
            Icon: "🔋",
            Category: "电气测控",
            Description: "标准 5A 电流互感器输出相电流监视，带对称超限警报",
            Apply: vm =>
            {
                if (vm is ArcGaugeWidgetViewModel g)
                {
                    g.Title = "相电流 RMS";
                    g.Unit = "ARMS";
                    g.MinValue = -5.0;
                    g.MaxValue = 5.0;
                    g.ColorHex = "#0284C7";
                    g.Decimals = 2;
                    g.HighAlarm = 4.5;
                    g.LowAlarm = -4.5;
                    g.UpdateScaleGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "ArcGauge_Following_Error_50",
            Type: WidgetType.GaugeArc,
            Name: "伺服轴位置跟随误差仪 (-50.0~50.0 μm)",
            Icon: "🎯",
            Category: "数控伺服",
            Description: "CNC 进给轴指令位置与编码器实际位置差值，超限触发轮廓超差报警",
            Apply: vm =>
            {
                if (vm is ArcGaugeWidgetViewModel g)
                {
                    g.Title = "X轴跟随误差";
                    g.Unit = "μm";
                    g.MinValue = -50.0;
                    g.MaxValue = 50.0;
                    g.ColorHex = "#06B6D4";
                    g.Decimals = 1;
                    g.HighAlarm = 35.0;
                    g.LowAlarm = -35.0;
                    g.UpdateScaleGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "ArcGauge_Torque_100",
            Type: WidgetType.GaugeArc,
            Name: "双向动力扭矩表 (-100~100 N·m)",
            Icon: "🔄",
            Category: "机械力学",
            Description: "主驱动轴正反向动态扭矩监测，高响应发光指针显示",
            Apply: vm =>
            {
                if (vm is ArcGaugeWidgetViewModel g)
                {
                    g.Title = "输出轴转矩";
                    g.Unit = "N·m";
                    g.MinValue = -100;
                    g.MaxValue = 100;
                    g.ColorHex = "#38BDF8";
                    g.Decimals = 1;
                    g.HighAlarm = 85.0;
                    g.LowAlarm = -85.0;
                    g.UpdateScaleGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "ArcGauge_Force_Tension_50",
            Type: WidgetType.GaugeArc,
            Name: "双向进给拉压力表 (-50.0~50.0 kN)",
            Icon: "⚓",
            Category: "机械力学",
            Description: "伺服压机/液压油缸推拉力双向监测，±50 kN 动态测力",
            Apply: vm =>
            {
                if (vm is ArcGaugeWidgetViewModel g)
                {
                    g.Title = "推力/拉力";
                    g.Unit = "kN";
                    g.MinValue = -50.0;
                    g.MaxValue = 50.0;
                    g.ColorHex = "#F59E0B";
                    g.Decimals = 1;
                    g.HighAlarm = 45.0;
                    g.LowAlarm = -45.0;
                    g.UpdateScaleGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "ArcGauge_Spindle_Load_150",
            Type: WidgetType.GaugeArc,
            Name: "主轴负载率监测表 (0~150 %)",
            Icon: "📊",
            Category: "数控伺服",
            Description: "加工中心主轴切削功率负载率，100%额定，120%过载保护",
            Apply: vm =>
            {
                if (vm is ArcGaugeWidgetViewModel g)
                {
                    g.Title = "主轴负载率";
                    g.Unit = "%";
                    g.MinValue = 0;
                    g.MaxValue = 150;
                    g.ColorHex = "#10B981";
                    g.Decimals = 1;
                    g.HighAlarm = 120.0;
                    g.LowAlarm = null;
                    g.UpdateScaleGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "ArcGauge_Diff_Pressure_100",
            Type: WidgetType.GaugeArc,
            Name: "管路双向微差压表 (-100~100 kPa)",
            Icon: "💨",
            Category: "压力流体",
            Description: "洁净室正负微差压/过滤器前后阻力差压监视",
            Apply: vm =>
            {
                if (vm is ArcGaugeWidgetViewModel g)
                {
                    g.Title = "滤芯两端差压";
                    g.Unit = "kPa";
                    g.MinValue = -100;
                    g.MaxValue = 100;
                    g.ColorHex = "#14B8A6";
                    g.Decimals = 1;
                    g.HighAlarm = 75.0;
                    g.LowAlarm = -75.0;
                    g.UpdateScaleGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "ArcGauge_Vacuum_Pressure",
            Type: WidgetType.GaugeArc,
            Name: "工业负压真空计 (-100.0~0.0 kPa)",
            Icon: "🕳️",
            Category: "压力流体",
            Description: "真空吸盘/负压脱气仓实时负压监控，-80 kPa 为达标阈值",
            Apply: vm =>
            {
                if (vm is ArcGaugeWidgetViewModel g)
                {
                    g.Title = "真空吸盘负压";
                    g.Unit = "kPa";
                    g.MinValue = -100.0;
                    g.MaxValue = 0.0;
                    g.ColorHex = "#60A5FA";
                    g.Decimals = 1;
                    g.LowAlarm = -20.0; // 负压不足报警
                    g.HighAlarm = null;
                    g.UpdateScaleGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "ArcGauge_Phase_Angle_90",
            Type: WidgetType.GaugeArc,
            Name: "电网相角差监视仪 (-90°~90°)",
            Icon: "📐",
            Category: "电气测控",
            Description: "微电网并网同步相角差/变频驱动电压电流相位差监测",
            Apply: vm =>
            {
                if (vm is ArcGaugeWidgetViewModel g)
                {
                    g.Title = "并网相角差";
                    g.Unit = "°";
                    g.MinValue = -90.0;
                    g.MaxValue = 90.0;
                    g.ColorHex = "#8B5CF6";
                    g.Decimals = 1;
                    g.HighAlarm = 30.0;
                    g.LowAlarm = -30.0;
                    g.UpdateScaleGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "ArcGauge_Air_Velocity_60",
            Type: WidgetType.GaugeArc,
            Name: "通风管道气流风速仪 (0~60 m/s)",
            Icon: "🌬️",
            Category: "动力环境",
            Description: "主风道送排风流速在线监视，超45 m/s高速风噪预警",
            Apply: vm =>
            {
                if (vm is ArcGaugeWidgetViewModel g)
                {
                    g.Title = "主风管风速";
                    g.Unit = "m/s";
                    g.MinValue = 0;
                    g.MaxValue = 60;
                    g.ColorHex = "#2DD4BF";
                    g.Decimals = 1;
                    g.HighAlarm = 45.0;
                    g.LowAlarm = 5.0;
                    g.UpdateScaleGeometry();
                }
            }
        ));
    }
    #endregion

    #region 普通按钮预设 (ControlButton)
    private static void RegisterControlButtonPresets()
    {
        Register(new WidgetStylePreset(
            Id: "Btn_Start_Success",
            Type: WidgetType.ControlButton,
            Name: "启动 / 运行按钮 (工业绿)",
            Icon: "🟢",
            Category: "常用控制",
            Description: "下发启动指令 (DirectWrite 1)，标准绿色高亮工业操作按键",
            Apply: vm =>
            {
                if (vm is ControlButtonWidgetViewModel b)
                {
                    b.Title = "启动控制";
                    b.ButtonText = "启动";
                    b.ColorHex = "#16A34A";
                    b.ButtonMode = "DirectWrite";
                    b.WriteValue = "1";
                    b.RequireConfirm = false;
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Btn_Stop_Neutral",
            Type: WidgetType.ControlButton,
            Name: "停止 / 停机按钮 (石板灰)",
            Icon: "⚪",
            Category: "常用控制",
            Description: "下发停止指令 (DirectWrite 0)，稳健深灰色停机按键",
            Apply: vm =>
            {
                if (vm is ControlButtonWidgetViewModel b)
                {
                    b.Title = "停止控制";
                    b.ButtonText = "停止";
                    b.ColorHex = "#475569";
                    b.ButtonMode = "DirectWrite";
                    b.WriteValue = "0";
                    b.RequireConfirm = false;
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Btn_Emergency_Stop",
            Type: WidgetType.ControlButton,
            Name: "紧急停止按钮 (急停红+确认)",
            Icon: "🔴",
            Category: "安全警示",
            Description: "安全急停切断指令 (DirectWrite 0)，带二次防误触确认弹窗",
            Apply: vm =>
            {
                if (vm is ControlButtonWidgetViewModel b)
                {
                    b.Title = "紧急停止";
                    b.ButtonText = "急停";
                    b.ColorHex = "#DC2626";
                    b.ButtonMode = "DirectWrite";
                    b.WriteValue = "0";
                    b.RequireConfirm = true;
                    b.ConfirmMessage = "确认触发紧急停止？设备将立即切断电源并停机！";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Btn_Alarm_Reset",
            Type: WidgetType.ControlButton,
            Name: "报警复位按钮 (琥珀黄)",
            Icon: "🟡",
            Category: "常用控制",
            Description: "复位报警故障脉冲 (Momentary 点动 1)，醒目警示黄色",
            Apply: vm =>
            {
                if (vm is ControlButtonWidgetViewModel b)
                {
                    b.Title = "故障复位";
                    b.ButtonText = "复位";
                    b.ColorHex = "#D97706";
                    b.ButtonMode = "Momentary";
                    b.WriteValue = "1";
                    b.RequireConfirm = false;
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Btn_Primary_Action",
            Type: WidgetType.ControlButton,
            Name: "主要操作按钮 (科技蓝)",
            Icon: "🔵",
            Category: "常用控制",
            Description: "参数下发/工序执行等核心主按钮，科技经典蓝色",
            Apply: vm =>
            {
                if (vm is ControlButtonWidgetViewModel b)
                {
                    b.Title = "操作确认";
                    b.ButtonText = "确定执行";
                    b.ColorHex = "#0284C7";
                    b.ButtonMode = "DirectWrite";
                    b.WriteValue = "1";
                    b.RequireConfirm = false;
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Btn_Toggle_Switch",
            Type: WidgetType.ControlButton,
            Name: "自锁启停开关 (紫罗兰)",
            Icon: "🟣",
            Category: "功能切换",
            Description: "点击翻转点位状态 (Toggle 0/1互锁切换)，适合照明/旁路",
            Apply: vm =>
            {
                if (vm is ControlButtonWidgetViewModel b)
                {
                    b.Title = "状态切换";
                    b.ButtonText = "启 / 停";
                    b.ColorHex = "#7C3AED";
                    b.ButtonMode = "Toggle";
                    b.WriteValue = "1";
                    b.RequireConfirm = false;
                }
            }
        ));
    }
    #endregion

    #region 液体储罐预设 (LevelTank)
    private static void RegisterTankLevelPresets()
    {
        Register(new WidgetStylePreset(
            Id: "Tank_Water_Vertical",
            Type: WidgetType.LevelTank,
            Name: "立式循环清水池 (0~100%)",
            Icon: "🚰",
            Category: "储罐水工",
            Description: "立式纯水/循环水箱，天蓝正常液体，低限20%干涸预警，高限85%溢流报警",
            Apply: vm =>
            {
                if (vm is TankLevelWidgetViewModel t)
                {
                    t.Title = "循环水箱液位";
                    t.Orientation = "Vertical";
                    t.Unit = "%";
                    t.MinValue = 0;
                    t.MaxValue = 100;
                    t.LowAlarm = 20;
                    t.HighAlarm = 85;
                    t.LowColor = "#EAB308";
                    t.MidColor = "#0284C7";
                    t.HighColor = "#EF4444";
                    t.ReevaluateLiquidColor();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Tank_Oil_Horizontal",
            Type: WidgetType.LevelTank,
            Name: "卧式储油槽罐 (0~5000L)",
            Icon: "⛽",
            Category: "储罐油化",
            Description: "卧式润滑油/柴油槽罐，琥珀金黄液体，低限800L，高限4500L",
            Apply: vm =>
            {
                if (vm is TankLevelWidgetViewModel t)
                {
                    t.Title = "日用储油槽罐";
                    t.Orientation = "Horizontal";
                    t.Unit = "L";
                    t.MinValue = 0;
                    t.MaxValue = 5000;
                    t.LowAlarm = 800;
                    t.HighAlarm = 4500;
                    t.LowColor = "#DC2626";
                    t.MidColor = "#F59E0B";
                    t.HighColor = "#EF4444";
                    t.ReevaluateLiquidColor();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Tank_Chemical_Vertical",
            Type: WidgetType.LevelTank,
            Name: "立式药剂强酸罐 (0~500L)",
            Icon: "🧪",
            Category: "储罐油化",
            Description: "加药计量桶/化工反应罐，专用紫色液体，低限50L，高限450L",
            Apply: vm =>
            {
                if (vm is TankLevelWidgetViewModel t)
                {
                    t.Title = "加药计量中和罐";
                    t.Orientation = "Vertical";
                    t.Unit = "L";
                    t.MinValue = 0;
                    t.MaxValue = 500;
                    t.LowAlarm = 50;
                    t.HighAlarm = 450;
                    t.LowColor = "#F97316";
                    t.MidColor = "#9333EA";
                    t.HighColor = "#EF4444";
                    t.ReevaluateLiquidColor();
                }
            }
        ));
    }
    #endregion

    #region 工业指示灯预设 (StatusLed)
    private static void RegisterStatusLedPresets()
    {
        Register(new WidgetStylePreset(
            Id: "Led_Run_Green",
            Type: WidgetType.StatusLed,
            Name: "运行 / 就绪指示灯 (翠绿)",
            Icon: "🟢",
            Category: "状态指示",
            Description: "点亮亮绿 (#10B981)，熄灭暗灰 (#334155)，通用工况指示",
            Apply: vm =>
            {
                if (vm is StatusLedWidgetViewModel led)
                {
                    led.Title = "运行就绪指示";
                    led.ActiveColor = "#10B981";
                    led.InactiveColor = "#334155";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Led_Fault_Red",
            Type: WidgetType.StatusLed,
            Name: "故障 / 报警指示灯 (大红)",
            Icon: "🔴",
            Category: "状态指示",
            Description: "点亮报警红 (#EF4444)，熄灭正常灰 (#334155)，故障报警指示",
            Apply: vm =>
            {
                if (vm is StatusLedWidgetViewModel led)
                {
                    led.Title = "综合故障报警";
                    led.ActiveColor = "#EF4444";
                    led.InactiveColor = "#334155";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Led_Standby_Yellow",
            Type: WidgetType.StatusLed,
            Name: "待机 / 预警指示灯 (金黄)",
            Icon: "🟡",
            Category: "状态指示",
            Description: "点亮警告黄 (#F59E0B)，熄灭暗灰 (#334155)，预警或待机",
            Apply: vm =>
            {
                if (vm is StatusLedWidgetViewModel led)
                {
                    led.Title = "设备待机预警";
                    led.ActiveColor = "#F59E0B";
                    led.InactiveColor = "#334155";
                }
            }
        ));
    }
    #endregion

    #region 数显卡片预设 (NumericCard)
    private static void RegisterNumericCardPresets()
    {
        Register(new WidgetStylePreset(
            Id: "Card_Power_KW",
            Type: WidgetType.NumericCard,
            Name: "高亮动力功率卡片 (kW)",
            Icon: "⚡",
            Category: "数显卡片",
            Description: "荧光黄绿数显发光，1位小数，单位 kW",
            Apply: vm =>
            {
                if (vm is NumericCardWidgetViewModel c)
                {
                    c.Title = "机台总用电功率";
                    c.Unit = "kW";
                    c.Decimals = 1;
                    c.ColorHex = "#84CC16";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Card_Temp_C",
            Type: WidgetType.NumericCard,
            Name: "高精度温度数显 (℃)",
            Icon: "🌡️",
            Category: "数显卡片",
            Description: "科技青蓝数显，1位小数，单位 ℃",
            Apply: vm =>
            {
                if (vm is NumericCardWidgetViewModel c)
                {
                    c.Title = "工作区实时温度";
                    c.Unit = "℃";
                    c.Decimals = 1;
                    c.ColorHex = "#06B6D4";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Card_Speed_RPM",
            Type: WidgetType.NumericCard,
            Name: "电机转速数显 (rpm)",
            Icon: "🔄",
            Category: "数显卡片",
            Description: "经典深蓝数显，整型无小数位，单位 rpm",
            Apply: vm =>
            {
                if (vm is NumericCardWidgetViewModel c)
                {
                    c.Title = "驱动轴转速";
                    c.Unit = "rpm";
                    c.Decimals = 0;
                    c.ColorHex = "#0284C7";
                }
            }
        ));
    }
    #endregion

    #region 实时趋势图预设 (TrendChart)
    private static void RegisterTrendChartPresets()
    {
        Register(new WidgetStylePreset(
            Id: "Trend_Reflow_Temp",
            Type: WidgetType.TrendChart,
            Name: "热工炉膛温区曲线 (0~300℃)",
            Icon: "🔥",
            Category: "趋势走势",
            Description: "温区波形走势，橙红警示主线，深红渐变底衬，0~300℃",
            Apply: vm =>
            {
                if (vm is TrendChartWidgetViewModel tc)
                {
                    tc.Title = "炉膛温区实时曲线";
                    tc.Unit = "℃";
                    tc.MinValue = 0;
                    tc.MaxValue = 300;
                    tc.LineColor = "#F97316"; // 橙红
                    tc.FillColor = "#7C2D12"; // 渐变底衬
                    tc.GridColor = "#1E293B";
                    tc.TimeWindowSeconds = 60;
                    tc.RebuildChartGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Trend_Motor_Current",
            Type: WidgetType.TrendChart,
            Name: "电机负荷电流曲线 (0~100A)",
            Icon: "⚡",
            Category: "趋势走势",
            Description: "主电机实时负载电流波动，荧光绿主线与微深底衬，0~100A",
            Apply: vm =>
            {
                if (vm is TrendChartWidgetViewModel tc)
                {
                    tc.Title = "主电机负载电流走势";
                    tc.Unit = "A";
                    tc.MinValue = 0;
                    tc.MaxValue = 100;
                    tc.LineColor = "#10B981"; // 翠绿
                    tc.FillColor = "#064E3B";
                    tc.GridColor = "#1E293B";
                    tc.TimeWindowSeconds = 60;
                    tc.RebuildChartGeometry();
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Trend_Hydraulic_Pressure",
            Type: WidgetType.TrendChart,
            Name: "液压瞬态脉冲压力 (0~25MPa)",
            Icon: "🌊",
            Category: "趋势走势",
            Description: "高压液压管路压力脉冲，科技蓝主线与深海蓝底衬，0~25MPa",
            Apply: vm =>
            {
                if (vm is TrendChartWidgetViewModel tc)
                {
                    tc.Title = "系统油压脉冲波形";
                    tc.Unit = "MPa";
                    tc.MinValue = 0;
                    tc.MaxValue = 25;
                    tc.LineColor = "#38BDF8"; // 科技天蓝
                    tc.FillColor = "#0C4A6E";
                    tc.GridColor = "#1E293B";
                    tc.TimeWindowSeconds = 30;
                    tc.RebuildChartGeometry();
                }
            }
        ));
    }
    #endregion

    #region IO 点阵状态板预设 (IoMatrix)
    private static void RegisterIoMatrixPresets()
    {
        Register(new WidgetStylePreset(
            Id: "Io_DI_8Ch_Status",
            Type: WidgetType.IoMatrix,
            Name: "8路 PLC 数字量输入板 (DI)",
            Icon: "🎛️",
            Category: "数字IO",
            Description: "8路光耦限位与就绪输入状态板（1组×8点，280×115），翠绿导通激活，HEX 格式",
            Apply: vm =>
            {
                if (vm is IoMatrixWidgetViewModel io)
                {
                    io.Title = "工位 DI 信号状态板";
                    io.IoChannels = 8;
                    io.Width = 280;
                    io.Height = 115;
                    io.DisplayFormat = "HEX";
                    io.ActiveColor = "#10B981";
                    io.InactiveColor = "#334155";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Io_DO_16Ch_Relay",
            Type: WidgetType.IoMatrix,
            Name: "16路 继电器输出控制板 (DO)",
            Icon: "⚡",
            Category: "数字IO",
            Description: "16路控制电磁阀/驱动输出点阵（2组×8点，280×160），琥珀金黄激活，BIN 二进制格式",
            Apply: vm =>
            {
                if (vm is IoMatrixWidgetViewModel io)
                {
                    io.Title = "阀岛 DO 输出控制板";
                    io.IoChannels = 16;
                    io.Width = 280;
                    io.Height = 160;
                    io.DisplayFormat = "BIN";
                    io.ActiveColor = "#F59E0B";
                    io.InactiveColor = "#334155";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Io_32Ch_DWord_Bus",
            Type: WidgetType.IoMatrix,
            Name: "32路 总线分布式IO点阵 (DWord)",
            Icon: "🌐",
            Category: "数字IO",
            Description: "32路现场总线扩展IO点阵（4组×8点，280×260），荧光青蓝激活，HEX 8位十六进制格式",
            Apply: vm =>
            {
                if (vm is IoMatrixWidgetViewModel io)
                {
                    io.Title = "现场总线 IO 站状态";
                    io.IoChannels = 32;
                    io.Width = 280;
                    io.Height = 260;
                    io.DisplayFormat = "HEX";
                    io.ActiveColor = "#38BDF8";
                    io.InactiveColor = "#1E293B";
                }
            }
        ));
    }
    #endregion

    #region 文本标签预设 (TextLabel - 纯静态标签展示，无需绑定采集点位)
    private static void RegisterTextLabelPresets()
    {
        Register(new WidgetStylePreset(
            Id: "Label_Screen_Main_Title",
            Type: WidgetType.TextLabel,
            Name: "大屏主控全局大标题 (22px 霓虹蓝)",
            Icon: "🖥️",
            Category: "全局标题",
            Description: "科技天蓝 22 号大字加粗，居中对齐，适合大屏顶部总控制看板名称",
            Apply: vm =>
            {
                if (vm is TextLabelWidgetViewModel lbl)
                {
                    lbl.Title = "产线监控大屏";
                    lbl.Text = "智能数字孪生产线总监控看板";
                    lbl.Width = 420;
                    lbl.Height = 48;
                    lbl.LabelFontSize = 22;
                    lbl.IsBold = true;
                    lbl.TextColor = "#38BDF8";
                    lbl.TextAlignment = "Center";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Label_Station_Header",
            Type: WidgetType.TextLabel,
            Name: "工位工艺高亮标题板 (18px 青蓝)",
            Icon: "🏷️",
            Category: "工位区域",
            Description: "科技青蓝 18 号加粗标题，居中对齐，工位铭牌与区域标题",
            Apply: vm =>
            {
                if (vm is TextLabelWidgetViewModel lbl)
                {
                    lbl.Title = "工位区域标题";
                    lbl.Text = "ST-01 装配加工工作站";
                    lbl.LabelFontSize = 18;
                    lbl.IsBold = true;
                    lbl.TextColor = "#38BDF8";
                    lbl.TextAlignment = "Center";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Label_Section_Subheader",
            Type: WidgetType.TextLabel,
            Name: "参数分组前缀小节标题 (13px 灰白)",
            Icon: "📑",
            Category: "工位区域",
            Description: "浅蓝灰 13 号加粗带引导符号，适合卡片群组或控制参数分组标题",
            Apply: vm =>
            {
                if (vm is TextLabelWidgetViewModel lbl)
                {
                    lbl.Title = "参数分组";
                    lbl.Text = "▸ 主回路温控与动力参数";
                    lbl.Width = 200;
                    lbl.Height = 32;
                    lbl.LabelFontSize = 13;
                    lbl.IsBold = true;
                    lbl.TextColor = "#94A3B8";
                    lbl.TextAlignment = "Left";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Label_Device_Tag_Nameplate",
            Type: WidgetType.TextLabel,
            Name: "设备资产位号铭牌 (12px 科技紫)",
            Icon: "🆔",
            Category: "设备铭牌",
            Description: "科技紫 12 号等宽居中，适合设备出厂编号、资产编码与位号标记",
            Apply: vm =>
            {
                if (vm is TextLabelWidgetViewModel lbl)
                {
                    lbl.Title = "设备位号";
                    lbl.Text = "TAG: CNC-SPINDLE-M01";
                    lbl.Width = 190;
                    lbl.Height = 32;
                    lbl.LabelFontSize = 12;
                    lbl.IsBold = true;
                    lbl.TextColor = "#A78BFA";
                    lbl.TextAlignment = "Center";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Label_State_Normal_Ready",
            Type: WidgetType.TextLabel,
            Name: "系统就绪状态常驻标签 (13px 工业绿)",
            Icon: "🟢",
            Category: "工况指示",
            Description: "工控翡翠绿 13 号，带状态圆点，静态标注设备就绪与安全连锁状态",
            Apply: vm =>
            {
                if (vm is TextLabelWidgetViewModel lbl)
                {
                    lbl.Title = "运行就绪";
                    lbl.Text = "● 系统运行正常 (ALL READY)";
                    lbl.Width = 210;
                    lbl.Height = 34;
                    lbl.LabelFontSize = 13;
                    lbl.IsBold = true;
                    lbl.TextColor = "#10B981";
                    lbl.TextAlignment = "Left";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Label_Safety_Danger_Alert",
            Type: WidgetType.TextLabel,
            Name: "危险禁入红色高亮警示 (14px 警报红)",
            Icon: "🛑",
            Category: "安全警示",
            Description: "高警示赤红 14 号加粗，醒目标注强电危险、激光或机械运动死区",
            Apply: vm =>
            {
                if (vm is TextLabelWidgetViewModel lbl)
                {
                    lbl.Title = "危险警示";
                    lbl.Text = "DANGER 危险：高压区域 严禁带电检修！";
                    lbl.Width = 280;
                    lbl.Height = 36;
                    lbl.LabelFontSize = 14;
                    lbl.IsBold = true;
                    lbl.TextColor = "#EF4444";
                    lbl.TextAlignment = "Left";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Label_Warning_Note",
            Type: WidgetType.TextLabel,
            Name: "操作安全须知提示 (13px 琥珀金黄)",
            Icon: "⚠️",
            Category: "安全警示",
            Description: "醒目金黄 13 号加粗提示文本，操作安全须知与警戒规程",
            Apply: vm =>
            {
                if (vm is TextLabelWidgetViewModel lbl)
                {
                    lbl.Title = "操作提示";
                    lbl.Text = "注意：设备运行时严禁触碰安全光幕！";
                    lbl.Width = 260;
                    lbl.Height = 36;
                    lbl.LabelFontSize = 13;
                    lbl.IsBold = true;
                    lbl.TextColor = "#F59E0B";
                    lbl.TextAlignment = "Left";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Label_EStop_Instructions",
            Type: WidgetType.TextLabel,
            Name: "急停与复位操作规程 (12px 玫瑰红)",
            Icon: "⛔",
            Category: "安全警示",
            Description: "玫瑰红 12 号加粗，引导操作人员顺时针旋转释放急停并复位",
            Apply: vm =>
            {
                if (vm is TextLabelWidgetViewModel lbl)
                {
                    lbl.Title = "复位操作指南";
                    lbl.Text = "急停触发后请顺时针旋转释放，并点击上位机复位";
                    lbl.Width = 280;
                    lbl.Height = 36;
                    lbl.LabelFontSize = 12;
                    lbl.IsBold = true;
                    lbl.TextColor = "#FB7185";
                    lbl.TextAlignment = "Left";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Label_Cleanroom_Spec",
            Type: WidgetType.TextLabel,
            Name: "车间环境基准规范 (12px 薄荷绿)",
            Icon: "🌿",
            Category: "工艺说明",
            Description: "极光薄荷绿 12 号，提示无尘车间标准温湿度基准与合格范围",
            Apply: vm =>
            {
                if (vm is TextLabelWidgetViewModel lbl)
                {
                    lbl.Title = "环境标准";
                    lbl.Text = "万级洁净室基准: 22±1℃ / 50±5%RH";
                    lbl.Width = 240;
                    lbl.Height = 32;
                    lbl.LabelFontSize = 12;
                    lbl.IsBold = false;
                    lbl.TextColor = "#2DD4BF";
                    lbl.TextAlignment = "Left";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Label_Batch_Workorder",
            Type: WidgetType.TextLabel,
            Name: "生产工单与批次说明 (12px 浅灰白)",
            Icon: "📋",
            Category: "生产信息",
            Description: "浅灰白 12 号，标注当前正在执行的生产工单编号与工件批号",
            Apply: vm =>
            {
                if (vm is TextLabelWidgetViewModel lbl)
                {
                    lbl.Title = "当前批次";
                    lbl.Text = "工单: WO-2026-0923 | 批号: LOT-B08";
                    lbl.Width = 240;
                    lbl.Height = 32;
                    lbl.LabelFontSize = 12;
                    lbl.IsBold = false;
                    lbl.TextColor = "#CBD5E1";
                    lbl.TextAlignment = "Left";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Label_Fieldbus_Channel",
            Type: WidgetType.TextLabel,
            Name: "现场总线通信标注 (11px 拓扑深灰)",
            Icon: "🔌",
            Category: "通信标注",
            Description: "深炭灰 11 号右对齐，适合角落弱化标注现场总线名称、波特率与端口",
            Apply: vm =>
            {
                if (vm is TextLabelWidgetViewModel lbl)
                {
                    lbl.Title = "通信端口";
                    lbl.Text = "BUS: PROFINET RT / Port: 502";
                    lbl.Width = 190;
                    lbl.Height = 28;
                    lbl.LabelFontSize = 11;
                    lbl.IsBold = false;
                    lbl.TextColor = "#64748B";
                    lbl.TextAlignment = "Right";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Label_Version_Watermark",
            Type: WidgetType.TextLabel,
            Name: "系统版本与版权水印 (10px 弱化灰)",
            Icon: "©️",
            Category: "全局标题",
            Description: "弱化深灰 10 号小字右对齐，适合画面右下角版权与版本标识",
            Apply: vm =>
            {
                if (vm is TextLabelWidgetViewModel lbl)
                {
                    lbl.Title = "版本信息";
                    lbl.Text = "Universal SCADA HMI v2.4.0 (Build 2026.09)";
                    lbl.Width = 260;
                    lbl.Height = 26;
                    lbl.LabelFontSize = 10;
                    lbl.IsBold = false;
                    lbl.TextColor = "#475569";
                    lbl.TextAlignment = "Right";
                }
            }
        ));
    }
    #endregion

    #region 普通显示框预设 (DisplayBox)
    private static void RegisterDisplayBoxPresets()
    {
        Register(new WidgetStylePreset(
            Id: "Display_System_State",
            Type: WidgetType.DisplayBox,
            Name: "深色高亮状态指示框",
            Icon: "🔲",
            Category: "数显展示",
            Description: "深色科技底座，天蓝前缀标签与大字状态数值",
            Apply: vm =>
            {
                if (vm is DisplayBoxWidgetViewModel db)
                {
                    db.Title = "系统当前状态";
                    db.Prefix = "工况模式:";
                    db.Unit = "";
                    db.Decimals = 0;
                    db.TextColor = "#38BDF8";
                    db.BorderColor = "#0284C7";
                    db.BackgroundColor = "#0F172A";
                    db.TextAlignment = "Right";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Display_Compact_Value",
            Type: WidgetType.DisplayBox,
            Name: "紧凑型测控参数显示框",
            Icon: "📊",
            Category: "数显展示",
            Description: "石板灰微边框，翠绿高亮数显，保留 2 位小数",
            Apply: vm =>
            {
                if (vm is DisplayBoxWidgetViewModel db)
                {
                    db.Title = "精细采样测量";
                    db.Prefix = "当前值:";
                    db.Unit = "mm";
                    db.Decimals = 2;
                    db.TextColor = "#10B981";
                    db.BorderColor = "#334155";
                    db.BackgroundColor = "#0B0F19";
                    db.TextAlignment = "Right";
                }
            }
        ));
    }
    #endregion

    #region 容器分组框预设 (PanelContainer)
    private static void RegisterPanelContainerPresets()
    {
        Register(new WidgetStylePreset(
            Id: "Panel_Control_Zone",
            Type: WidgetType.PanelContainer,
            Name: "主控动力电气分区容器",
            Icon: "📦",
            Category: "容器分组",
            Description: "深蓝半透明背景底板，天蓝微边框，带工位分区顶栏",
            Apply: vm =>
            {
                if (vm is PanelContainerWidgetViewModel p)
                {
                    p.Title = "动力电气区";
                    p.GroupTitle = "动力与变频驱动单元";
                    p.HeaderBgColor = "#1E293B";
                    p.BorderColor = "#0284C7";
                    p.FillColor = "#0A0F1D";
                    p.CornerRadius = 8;
                    p.BorderThickness = 1.5;
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Panel_Alarm_Zone",
            Type: WidgetType.PanelContainer,
            Name: "安全警示联动分区容器",
            Icon: "🚨",
            Category: "容器分组",
            Description: "警示暗红微底板，琥珀黄微发光边框，安全联锁与保护分区",
            Apply: vm =>
            {
                if (vm is PanelContainerWidgetViewModel p)
                {
                    p.Title = "安全防护区";
                    p.GroupTitle = "安全联锁与急停防护区";
                    p.HeaderBgColor = "#3F1515";
                    p.BorderColor = "#F59E0B";
                    p.FillColor = "#1A0A0A";
                    p.CornerRadius = 8;
                    p.BorderThickness = 1.5;
                }
            }
        ));
    }
    #endregion

    #region 工艺管道预设 (Pipe)
    private static void RegisterPipePresets()
    {
        Register(new WidgetStylePreset(
            Id: "Pipe_Cooling_Water",
            Type: WidgetType.Pipe,
            Name: "循环冷却水管道 (横向水蓝流动)",
            Icon: "🌊",
            Category: "管道流体",
            Description: "标准横向供水/循环水管，水蓝介质动态跑马灯流动，周期 1.5s",
            Apply: vm =>
            {
                if (vm is PipeWidgetViewModel p)
                {
                    p.Title = "循环水管路";
                    p.Orientation = "Horizontal";
                    p.LiquidColor = "#0284C7";
                    p.PipeColor = "#1E293B";
                    p.FlowDirection = "Forward";
                    p.FlowSpeed = 1.5;
                    p.IsFlowing = true;
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Pipe_Fuel_Oil",
            Type: WidgetType.Pipe,
            Name: "重油/燃油管道 (横向琥珀金流动)",
            Icon: "⛽",
            Category: "管道流体",
            Description: "润滑油/燃油管路，琥珀金黄介质跑马灯流动，周期 2.5s",
            Apply: vm =>
            {
                if (vm is PipeWidgetViewModel p)
                {
                    p.Title = "燃油进油管路";
                    p.Orientation = "Horizontal";
                    p.LiquidColor = "#F59E0B";
                    p.PipeColor = "#1E293B";
                    p.FlowDirection = "Forward";
                    p.FlowSpeed = 2.5;
                    p.IsFlowing = true;
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Pipe_Vertical_Steam",
            Type: WidgetType.Pipe,
            Name: "立式蒸汽管道 (纵向气白流动)",
            Icon: "💨",
            Category: "管道流体",
            Description: "立式气动/高温蒸汽输送管路，气白高速流动，周期 1.0s",
            Apply: vm =>
            {
                if (vm is PipeWidgetViewModel p)
                {
                    p.Title = "立式蒸汽管路";
                    p.Orientation = "Vertical";
                    p.LiquidColor = "#E2E8F0";
                    p.PipeColor = "#334155";
                    p.FlowDirection = "Forward";
                    p.FlowSpeed = 1.0;
                    p.IsFlowing = true;
                }
            }
        ));
    }
    #endregion

    #region 设备状态卡片预设 (DeviceStatus)
    private static void RegisterDeviceStatusPresets()
    {
        Register(new WidgetStylePreset(
            Id: "Dev_Standard_Card",
            Type: WidgetType.DeviceStatus,
            Name: "工业标准通信卡片 (280×140)",
            Icon: "🖥️",
            Category: "通信拓扑",
            Description: "标准工控通信卡片，展示协议徽标、通道名称、站号、轮询周期与延时",
            Apply: vm =>
            {
                if (vm is DeviceStatusWidgetViewModel dev)
                {
                    dev.Title = "PLC 主控制器";
                    dev.Width = 280;
                    dev.Height = 140;
                    dev.DeviceName = "主线生产PLC";
                    dev.ConnectionStatus = "Online";
                    dev.LatencyMs = 12;
                    dev.IsCompact = false;
                    dev.ActiveColor = "#10B981";
                    dev.OfflineColor = "#EF4444";
                    dev.WarningColor = "#F59E0B";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Dev_Compact_Tag",
            Type: WidgetType.DeviceStatus,
            Name: "密集机柜紧凑标签 (220×90)",
            Icon: "🏷️",
            Category: "通信拓扑",
            Description: "紧凑微型设备标签，适合多设备密集排列的高密度监控画面",
            Apply: vm =>
            {
                if (vm is DeviceStatusWidgetViewModel dev)
                {
                    dev.Title = "温湿度传感器模块";
                    dev.Width = 220;
                    dev.Height = 90;
                    dev.DeviceName = "环境监测仪";
                    dev.ConnectionStatus = "Online";
                    dev.LatencyMs = 8;
                    dev.IsCompact = true;
                    dev.ActiveColor = "#06B6D4";
                    dev.OfflineColor = "#EF4444";
                    dev.WarningColor = "#F59E0B";
                }
            }
        ));

        Register(new WidgetStylePreset(
            Id: "Dev_Server_Panel",
            Type: WidgetType.DeviceStatus,
            Name: "核心服务器监视面板 (320×175)",
            Icon: "🖧",
            Category: "通信拓扑",
            Description: "大型主站设备面板，高对比发光指示，突出测点规模与链路质量",
            Apply: vm =>
            {
                if (vm is DeviceStatusWidgetViewModel dev)
                {
                    dev.Title = "SCADA 数据采集服务器";
                    dev.Width = 320;
                    dev.Height = 175;
                    dev.DeviceName = "SCADA_IO_SERVER";
                    dev.ConnectionStatus = "Online";
                    dev.LatencyMs = 5;
                    dev.IsCompact = false;
                    dev.ActiveColor = "#38BDF8";
                    dev.OfflineColor = "#DC2626";
                    dev.WarningColor = "#EAB308";
                }
            }
        ));
    }
    #endregion
}


