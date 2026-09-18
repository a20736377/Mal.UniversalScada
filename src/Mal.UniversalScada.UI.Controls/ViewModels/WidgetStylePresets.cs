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
        RegisterControlButtonPresets();
        RegisterTankLevelPresets();
        RegisterStatusLedPresets();
        RegisterNumericCardPresets();
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
}
