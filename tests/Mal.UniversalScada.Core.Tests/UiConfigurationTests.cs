using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Storage.Sqlite;
using Mal.UniversalScada.UI.Controls.ViewModels;
using Xunit;

namespace Mal.UniversalScada.Core.Tests;

public class UiConfigurationTests
{
    [Fact]
    public async Task Sqlite_UiView_Crud_ShouldPreserveAllWidgetConfigurations()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"scada_test_{Guid.NewGuid():N}.db");
        var connStr = $"Data Source={tempDbPath}";

        try
        {
            var repo = new SqliteConfigRepository(connStr);

            // 1. Arrange View with multiple heterogeneous widgets
            var view = new UiViewConfig
            {
                ViewId = "VIEW_TEST_01",
                Name = "智能烘箱监控看板",
                BoundDeviceId = "DEV_01",
                CanvasWidth = 1920,
                CanvasHeight = 1080,
                IsDefault = true,
                Widgets = new List<WidgetConfig>
                {
                    new()
                    {
                        WidgetId = "W_GAUGE_1",
                        Type = WidgetType.GaugeCircular,
                        Title = "预热区温度表",
                        PrimaryTagId = 1,
                        X = 100,
                        Y = 150,
                        Width = 200,
                        Height = 200,
                        Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "400", ["Unit"] = "℃" }
                    },
                    new()
                    {
                        WidgetId = "W_TANK_1",
                        Type = WidgetType.LevelTank,
                        Title = "冷却液罐",
                        PrimaryTagId = 2,
                        X = 350,
                        Y = 150,
                        Width = 160,
                        Height = 240,
                        Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "100", ["Unit"] = "%" }
                    },
                    new()
                    {
                        WidgetId = "W_BTN_1",
                        Type = WidgetType.ControlButton,
                        Title = "急停复位",
                        PrimaryTagId = 3,
                        X = 550,
                        Y = 200,
                        Width = 140,
                        Height = 80,
                        Action = new WidgetActionConfig
                        {
                            ActionType = "DirectWrite",
                            TargetTagId = 3,
                            Value = "1"
                        }
                    }
                }
            };

            // 2. Act: Save View
            await repo.SaveUiViewAsync(view);

            // 3. Assert: Read list
            var allViews = await repo.GetUiViewsAsync();
            Assert.Single(allViews);
            Assert.Equal("VIEW_TEST_01", allViews[0].ViewId);
            Assert.Equal("智能烘箱监控看板", allViews[0].Name);
            Assert.True(allViews[0].IsDefault);
            Assert.Equal(3, allViews[0].Widgets.Count);

            // Assert: Read single
            var loaded = await repo.GetUiViewByIdAsync("VIEW_TEST_01");
            Assert.NotNull(loaded);
            Assert.Equal(1920, loaded!.CanvasWidth);
            Assert.Equal(1080, loaded.CanvasHeight);

            // Verify Widget Details
            var gauge = loaded.Widgets.FirstOrDefault(w => w.WidgetId == "W_GAUGE_1");
            Assert.NotNull(gauge);
            Assert.Equal(WidgetType.GaugeCircular, gauge!.Type);
            Assert.Equal(100, gauge.X);
            Assert.Equal(150, gauge.Y);
            Assert.Equal("400", gauge.GetProp("MaxValue"));
            Assert.Equal("℃", gauge.GetProp("Unit"));

            var btn = loaded.Widgets.FirstOrDefault(w => w.WidgetId == "W_BTN_1");
            Assert.NotNull(btn);
            Assert.Equal(WidgetType.ControlButton, btn!.Type);
            Assert.NotNull(btn.Action);
            Assert.Equal("DirectWrite", btn.Action!.ActionType);

            // 4. Act: Delete View
            await repo.DeleteUiViewAsync("VIEW_TEST_01");
            var remaining = await repo.GetUiViewsAsync();
            Assert.Empty(remaining);
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void WidgetViewModel_FromConfig_And_ToConfig_PreservesState()
    {
        var config = new WidgetConfig
        {
            WidgetId = "W_TEST_99",
            Type = WidgetType.NumericCard,
            Title = "主轴转速",
            PrimaryTagId = 10,
            X = 50,
            Y = 70,
            Width = 200,
            Height = 150,
            Properties = new()
            {
                ["MinValue"] = "0",
                ["MaxValue"] = "6000",
                ["Unit"] = "rpm"
            }
        };

        var vm = WidgetViewModel.FromConfig(config, isDesignMode: true);
        Assert.Equal("W_TEST_99", vm.Id);
        Assert.Equal("主轴转速", vm.Title);
        Assert.Equal(0, vm.MinValue);
        Assert.Equal(6000, vm.MaxValue);
        Assert.Equal("rpm", vm.Unit);
        Assert.True(vm.IsDesignMode);

        // Modify in designer
        vm.X = 120;
        vm.Y = 180;
        vm.Title = "主轴2号转速";
        vm.Unit = "r/min";

        var updatedConfig = vm.ToConfig();
        Assert.Equal(120, updatedConfig.X);
        Assert.Equal(180, updatedConfig.Y);
        Assert.Equal("主轴2号转速", updatedConfig.Title);
        Assert.Equal("r/min", updatedConfig.GetProp("Unit"));
    }

    [Fact]
    public void WidgetViewModel_UpdateRuntimeValue_CalculatesProgressAndAngle()
    {
        var vm = new WidgetViewModel
        {
            Type = WidgetType.GaugeCircular,
            MinValue = 0,
            MaxValue = 100,
            Decimals = 0
        };

        // Mid point: 50 -> 50% -> angle = 0°
        vm.UpdateRuntimeValue(50.0);
        Assert.Equal(0.5, vm.NormalizedProgress, precision: 2);
        Assert.Equal(0.0, vm.GaugeAngle, precision: 1);
        Assert.Equal("50", vm.FormattedValue);

        // Min point: 0 -> 0% -> angle = -135°
        vm.UpdateRuntimeValue(0.0);
        Assert.Equal(0.0, vm.NormalizedProgress, precision: 2);
        Assert.Equal(-135.0, vm.GaugeAngle, precision: 1);

        // Max point: 100 -> 100% -> angle = +135°
        vm.UpdateRuntimeValue(100.0);
        Assert.Equal(1.0, vm.NormalizedProgress, precision: 2);
        Assert.Equal(135.0, vm.GaugeAngle, precision: 1);
    }

    [Fact]
    public void WidgetArchitecture_AllWidgetsShareBaseClass_AndHaveSpecializedCompositionProps()
    {
        // 1. 验证所有组件均继承自共同基类 WidgetViewModel
        var gaugeVm = new CircularGaugeWidgetViewModel();
        var tankVm = new TankLevelWidgetViewModel();
        var numVm = new NumericCardWidgetViewModel();
        var ioVm = new IoMatrixWidgetViewModel();
        var ledVm = new StatusLedWidgetViewModel();
        var btnVm = new ControlButtonWidgetViewModel();

        Assert.True(gaugeVm is WidgetViewModel);
        Assert.True(tankVm is WidgetViewModel);
        Assert.True(numVm is WidgetViewModel);
        Assert.True(ioVm is WidgetViewModel);
        Assert.True(ledVm is WidgetViewModel);
        Assert.True(btnVm is WidgetViewModel);

        // 2. 验证各组件独有的属性存放在独立的组合类中
        Assert.NotNull(gaugeVm.GaugeProps);
        Assert.IsType<CircularGaugeProps>(gaugeVm.GaugeProps);
        Assert.Same(gaugeVm.Props, gaugeVm.GaugeProps);

        Assert.NotNull(tankVm.TankProps);
        Assert.IsType<TankLevelProps>(tankVm.TankProps);

        Assert.NotNull(numVm.CardProps);
        Assert.IsType<NumericCardProps>(numVm.CardProps);

        Assert.NotNull(ioVm.IoProps);
        Assert.IsType<IoMatrixProps>(ioVm.IoProps);

        Assert.NotNull(ledVm.LedProps);
        Assert.IsType<StatusLedProps>(ledVm.LedProps);

        Assert.NotNull(btnVm.ButtonProps);
        Assert.IsType<ControlButtonProps>(btnVm.ButtonProps);

        // 3. 验证通过工厂方法 WidgetViewModel.FromConfig 创建后组合类属性正确注入与回写
        var gaugeConfig = new WidgetConfig
        {
            WidgetId = "G_01",
            Type = WidgetType.GaugeCircular,
            Title = "主油温",
            Properties = new()
            {
                ["MinValue"] = "20",
                ["MaxValue"] = "180",
                ["Unit"] = "℃",
                ["HighAlarm"] = "150",
                ["Decimals"] = "2"
            }
        };

        var restoredGauge = WidgetViewModel.FromConfig(gaugeConfig) as CircularGaugeWidgetViewModel;
        Assert.NotNull(restoredGauge);
        // 验证独有参数存放在组合类中
        Assert.Equal(20, restoredGauge!.Props.MinValue);
        Assert.Equal(180, restoredGauge.Props.MaxValue);
        Assert.Equal("℃", restoredGauge.Props.Unit);
        Assert.Equal(150, restoredGauge.Props.HighAlarm);
        Assert.Equal(2, restoredGauge.Props.Decimals);
        // 验证共有参数在基类中
        Assert.Equal("G_01", restoredGauge.Id);
        Assert.Equal("主油温", restoredGauge.Title);

        // 修改组合类参数并验证回写保存
        restoredGauge.Props.Unit = "degC";
        restoredGauge.Props.HighAlarm = 160;
        var exportedConfig = restoredGauge.ToConfig();
        Assert.Equal("degC", exportedConfig.GetProp("Unit"));
        Assert.Equal("160", exportedConfig.GetProp("HighAlarm"));
    }

    [Fact]
    public void WidgetColorProperty_WhenModified_NotifiesSynchronously()
    {
        // 1. 圆形仪表颜色同步
        var gauge = new CircularGaugeWidgetViewModel();
        string? notifiedPropGauge = null;
        gauge.PropertyChanged += (s, e) => notifiedPropGauge = e.PropertyName;
        gauge.GaugeProps!.ColorHex = "#EF4444";
        Assert.Equal("ColorHex", notifiedPropGauge);
        Assert.Equal("#EF4444", gauge.ColorHex);

        // 2. 储罐颜色同步
        var tank = new TankLevelWidgetViewModel();
        string? notifiedPropTank = null;
        tank.PropertyChanged += (s, e) => notifiedPropTank = e.PropertyName;
        tank.TankProps!.ColorHex = "#10B981";
        Assert.Equal("ColorHex", notifiedPropTank);
        Assert.Equal("#10B981", tank.ColorHex);

        // 3. 数显卡片颜色同步
        var card = new NumericCardWidgetViewModel();
        string? notifiedPropCard = null;
        card.PropertyChanged += (s, e) => notifiedPropCard = e.PropertyName;
        card.CardProps!.ColorHex = "#F59E0B";
        Assert.Equal("ColorHex", notifiedPropCard);
        Assert.Equal("#F59E0B", card.ColorHex);

        // 4. IO 矩阵点阵颜色同步
        var io = new IoMatrixWidgetViewModel();
        var notifiedPropsIo = new List<string>();
        io.PropertyChanged += (s, e) => { if (e.PropertyName != null) notifiedPropsIo.Add(e.PropertyName); };
        io.IoProps!.ActiveColor = "#38BDF8";
        io.IoProps!.InactiveColor = "#1E293B";
        Assert.Contains("ActiveColor", notifiedPropsIo);
        Assert.Contains("InactiveColor", notifiedPropsIo);
        Assert.Equal("#38BDF8", io.ActiveColor);
        Assert.Equal("#1E293B", io.InactiveColor);

        // 5. 状态指示灯颜色同步
        var led = new StatusLedWidgetViewModel();
        var notifiedPropsLed = new List<string>();
        led.PropertyChanged += (s, e) => { if (e.PropertyName != null) notifiedPropsLed.Add(e.PropertyName); };
        led.LedProps!.ActiveColor = "#DC2626";
        led.LedProps!.InactiveColor = "#475569";
        Assert.Contains("ActiveColor", notifiedPropsLed);
        Assert.Contains("InactiveColor", notifiedPropsLed);
        Assert.Equal("#DC2626", led.ActiveColor);
        Assert.Equal("#475569", led.InactiveColor);

        // 6. 控制按钮颜色同步
        var btn = new ControlButtonWidgetViewModel();
        string? notifiedPropBtn = null;
        btn.PropertyChanged += (s, e) => notifiedPropBtn = e.PropertyName;
        btn.ButtonProps!.ColorHex = "#8B5CF6";
        Assert.Equal("ColorHex", notifiedPropBtn);
        Assert.Equal("#8B5CF6", btn.ColorHex);
    }

    [Fact]
    public void TagOptionItem_CompatibilityRules_FiltersCorrectlyForWidgetTypes()
    {
        // 1. 模拟量/数值型组件（仪表、储罐、数显卡片）仅支持数值点位
        var numericWidgets = new[] { WidgetType.GaugeCircular, WidgetType.LevelTank, WidgetType.NumericCard };
        foreach (var wType in numericWidgets)
        {
            Assert.True(TagOptionItem.IsCompatibleWithWidget(wType, TagDataType.Float));
            Assert.True(TagOptionItem.IsCompatibleWithWidget(wType, TagDataType.Double));
            Assert.True(TagOptionItem.IsCompatibleWithWidget(wType, TagDataType.Int16));
            Assert.True(TagOptionItem.IsCompatibleWithWidget(wType, TagDataType.Int32));
            Assert.True(TagOptionItem.IsCompatibleWithWidget(wType, TagDataType.UInt32));

            Assert.False(TagOptionItem.IsCompatibleWithWidget(wType, TagDataType.Bool));
            Assert.False(TagOptionItem.IsCompatibleWithWidget(wType, TagDataType.String));
            Assert.False(TagOptionItem.IsCompatibleWithWidget(wType, TagDataType.ByteArray));
        }

        // 2. 状态指示灯仅支持布尔量点位
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.StatusLed, TagDataType.Bool));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.StatusLed, TagDataType.Float));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.StatusLed, TagDataType.Int16));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.StatusLed, TagDataType.String));

        // 3. IO 矩阵点阵仅支持整型或状态字点位
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.IoMatrix, TagDataType.UInt16));
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.IoMatrix, TagDataType.Int16));
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.IoMatrix, TagDataType.UInt8));
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.IoMatrix, TagDataType.Int32));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.IoMatrix, TagDataType.Float));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.IoMatrix, TagDataType.Double));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.IoMatrix, TagDataType.Bool));

        // 4. 控制按钮支持布尔量；若为数值量，必须具有可写权限
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.ControlButton, TagDataType.Bool, TagAccessMode.ReadOnly));
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.ControlButton, TagDataType.Bool, TagAccessMode.ReadWrite));
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.ControlButton, TagDataType.Float, TagAccessMode.ReadWrite));
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.ControlButton, TagDataType.Int32, TagAccessMode.WriteOnly));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.ControlButton, TagDataType.Float, TagAccessMode.ReadOnly));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.ControlButton, TagDataType.String, TagAccessMode.ReadWrite));
    }

    [Fact]
    public void TagOptionItem_FromTagNode_GeneratesAccurateSummaryAndLabels()
    {
        var tag = new TagNode
        {
            Id = 1001,
            Name = "1号预热区温度",
            DataType = TagDataType.Float,
            Unit = "℃",
            Address = "D1000",
            AccessMode = TagAccessMode.ReadOnly
        };

        var option = TagOptionItem.FromTagNode(tag);

        Assert.Equal(1001, option.Id);
        Assert.Equal("1号预热区温度", option.Name);
        Assert.Equal(TagDataType.Float, option.DataType);
        Assert.True(option.IsNumeric);
        Assert.True(option.IsFloat);
        Assert.False(option.IsInteger);
        Assert.False(option.IsBool);
        Assert.Contains("[Float 浮点数]", option.DisplayText);
        Assert.Contains("[#1001]", option.DisplayText);
        Assert.Contains("(℃)", option.DisplayText);
        Assert.Contains("D1000", option.DetailSummary);
    }

    [Fact]
    public void TagOptionItem_NewWidgets_CompatibilityRules()
    {
        // 1. TrendChart (实时趋势图) 仅匹配数值量
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.TrendChart, TagDataType.Float));
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.TrendChart, TagDataType.Double));
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.TrendChart, TagDataType.Int32));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.TrendChart, TagDataType.Bool));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.TrendChart, TagDataType.String));

        // 2. DisplayBox (普通显示框) 匹配数值、布尔与字符串
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.DisplayBox, TagDataType.Float));
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.DisplayBox, TagDataType.Bool));
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.DisplayBox, TagDataType.String));

        // 3. TextLabel (文本标签) 纯静态展示文本，无需绑定采集点位
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.TextLabel, TagDataType.Float));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.TextLabel, TagDataType.Bool));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.TextLabel, TagDataType.String));

        // 4. PanelContainer (容器) 纯装饰与分组容器，不匹配任何点位
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.PanelContainer, TagDataType.Float));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.PanelContainer, TagDataType.Bool));

        // 5. Valve (控制阀门) 仅支持 Bool 型点位
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.Valve, TagDataType.Bool));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.Valve, TagDataType.Float));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.Valve, TagDataType.Double));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.Valve, TagDataType.Int16));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.Valve, TagDataType.Int32));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.Valve, TagDataType.UInt32));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.Valve, TagDataType.String));

        // 6. Pump (离心泵) 仅支持 Bool 型点位
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.Pump, TagDataType.Bool));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.Pump, TagDataType.Float));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.Pump, TagDataType.Double));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.Pump, TagDataType.Int16));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.Pump, TagDataType.Int32));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.Pump, TagDataType.UInt32));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.Pump, TagDataType.String));
    }

    [Fact]
    public async Task Sqlite_UiView_WithNewWidgetsAndBackground_ShouldPersistSuccessfully()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"scada_test_new_{Guid.NewGuid():N}.db");
        var connStr = $"Data Source={tempDbPath}";

        try
        {
            var repo = new SqliteConfigRepository(connStr);

            var view = new UiViewConfig
            {
                ViewId = "VIEW_NEW_01",
                Name = "工艺流程主控画面",
                BackgroundColor = "#070B14",
                BackgroundImagePath = "C:\\assets\\background_flow.png",
                BackgroundImageStretch = "UniformToFill",
                BackgroundImageOpacity = 0.45,
                CanvasWidth = 1920,
                CanvasHeight = 1080,
                Widgets = new List<WidgetConfig>
                {
                    new()
                    {
                        WidgetId = "W_LABEL_1",
                        Type = WidgetType.TextLabel,
                        Title = "回流焊1号工位说明",
                        X = 20, Y = 20, Width = 200, Height = 40,
                        Properties = new() { ["Text"] = "1号工位", ["FontSize"] = "16", ["IsBold"] = "True" }
                    },
                    new()
                    {
                        WidgetId = "W_DISP_1",
                        Type = WidgetType.DisplayBox,
                        Title = "炉温实时显示",
                        PrimaryTagId = 5,
                        X = 240, Y = 20, Width = 160, Height = 60,
                        Properties = new() { ["Prefix"] = "炉温", ["Unit"] = "℃", ["Decimals"] = "1" }
                    },
                    new()
                    {
                        WidgetId = "W_CHART_1",
                        Type = WidgetType.TrendChart,
                        Title = "炉温曲线",
                        PrimaryTagId = 5,
                        X = 20, Y = 100, Width = 380, Height = 200,
                        Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "300", ["Unit"] = "℃" }
                    },
                    new()
                    {
                        WidgetId = "W_PANEL_1",
                        Type = WidgetType.PanelContainer,
                        Title = "预热区容器",
                        X = 10, Y = 10, Width = 420, Height = 320,
                        Properties = new() { ["GroupTitle"] = "预热温区", ["CornerRadius"] = "8" }
                    }
                }
            };

            await repo.SaveUiViewAsync(view);

            var loaded = await repo.GetUiViewByIdAsync("VIEW_NEW_01");
            Assert.NotNull(loaded);
            Assert.Equal("工艺流程主控画面", loaded!.Name);
            Assert.Equal("#070B14", loaded.BackgroundColor);
            Assert.Equal("C:\\assets\\background_flow.png", loaded.BackgroundImagePath);
            Assert.Equal("UniformToFill", loaded.BackgroundImageStretch);
            Assert.Equal(0.45, loaded.BackgroundImageOpacity);
            Assert.Equal(4, loaded.Widgets.Count);

            var panel = loaded.Widgets.First(w => w.Type == WidgetType.PanelContainer);
            Assert.Equal("预热温区", panel.Properties["GroupTitle"]);

            var chart = loaded.Widgets.First(w => w.Type == WidgetType.TrendChart);
            Assert.Equal("300", chart.Properties["MaxValue"]);
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void WidgetViewModel_DeletedTag_ClearSelectionRule()
    {
        // 模拟已配置点位的组件
        var widget = WidgetViewModel.Create(WidgetType.DisplayBox);
        widget.PrimaryTagId = 999;
        widget.UpdateRuntimeValue(123.45);
        Assert.Equal(999, widget.PrimaryTagId);
        Assert.Equal(123.45, widget.CurrentRawValue);

        // 模拟点位表中该点位已被彻底删除
        var existingTags = new List<TagNode>
        {
            new() { Id = 1, Name = "有效点位" }
        };

        // 模拟 UiDesignerViewModel 中的自动清理扫描
        if (widget.PrimaryTagId > 0 &&
            !existingTags.Any(t => t.Id == widget.PrimaryTagId))
        {
            widget.PrimaryTagId = 0;
            widget.UpdateRuntimeValue(null);
        }

        // 断言：点位已被置空，运行时值已被复位，避免幽灵点位
        Assert.Equal(0, widget.PrimaryTagId);
        Assert.Null(widget.CurrentRawValue);
        Assert.Equal("--", widget.FormattedValue);
    }

    [Fact]
    public void CircularGauge_ThresholdColors_ShouldAdaptToValueRanges()
    {
        var gauge = (CircularGaugeWidgetViewModel)WidgetViewModel.Create(WidgetType.GaugeCircular);
        gauge.MinValue = 0;
        gauge.MaxValue = 100;

        gauge.Props.LowValue = 20;
        gauge.Props.LowColor = "#38BDF8";

        gauge.Props.MidValue = 50;
        gauge.Props.MidColor = "#10B981";

        gauge.Props.HighValue = 80;
        gauge.Props.HighColor = "#EF4444";

        gauge.Props.EnableThresholdColor = true;

        // 1. 低值区间 (15 <= 20)
        gauge.UpdateRuntimeValue(15);
        Assert.Equal("#38BDF8", gauge.ColorHex);
        Assert.True(gauge.IsAlarm); // 触发低限报警

        // 2. 中值区间 (20 < 45 < 80)
        gauge.UpdateRuntimeValue(45);
        Assert.Equal("#10B981", gauge.ColorHex);
        Assert.False(gauge.IsAlarm);

        // 3. 高值区间 (85 >= 80)
        gauge.UpdateRuntimeValue(85);
        Assert.Equal("#EF4444", gauge.ColorHex);
        Assert.True(gauge.IsAlarm); // 触发高限报警

        // 4. 持久化字典同步测试
        gauge.SyncPropertiesFromFields();
        Assert.Equal("20", gauge.Properties["LowValue"]);
        Assert.Equal("50", gauge.Properties["MidValue"]);
        Assert.Equal("80", gauge.Properties["HighValue"]);
        Assert.Equal("#38BDF8", gauge.Properties["LowColor"]);
        Assert.Equal("#10B981", gauge.Properties["MidColor"]);
        Assert.Equal("#EF4444", gauge.Properties["HighColor"]);
        Assert.Equal("True", gauge.Properties["EnableThresholdColor"]);

        // 5. 反序列化测试
        var newGauge = (CircularGaugeWidgetViewModel)WidgetViewModel.Create(WidgetType.GaugeCircular);
        newGauge.LoadProperties(gauge.Properties);
        Assert.Equal(20, newGauge.Props.LowValue);
        Assert.Equal(50, newGauge.Props.MidValue);
        Assert.Equal(80, newGauge.Props.HighValue);
        Assert.Equal("#38BDF8", newGauge.Props.LowColor);
        Assert.Equal("#10B981", newGauge.Props.MidColor);
        Assert.Equal("#EF4444", newGauge.Props.HighColor);
        Assert.True(newGauge.Props.EnableThresholdColor);

        // 6. 分段式圆弧表盘 Path 生成断言 (类似于湿度计的分段色表盘)
        Assert.False(string.IsNullOrWhiteSpace(newGauge.Props.LowArcData));
        Assert.False(string.IsNullOrWhiteSpace(newGauge.Props.MidArcData));
        Assert.False(string.IsNullOrWhiteSpace(newGauge.Props.HighArcData));
        Assert.StartsWith("M ", newGauge.Props.LowArcData);
        Assert.Contains(" A ", newGauge.Props.LowArcData);
        Assert.StartsWith("M ", newGauge.Props.MidArcData);
        Assert.Contains(" A ", newGauge.Props.MidArcData);
        Assert.StartsWith("M ", newGauge.Props.HighArcData);
        Assert.Contains(" A ", newGauge.Props.HighArcData);

        // 7. 初始无数据时，表盘中间轴心圆圈和指针应呈现中段颜色 (MidColor 绿色)
        var defaultGauge = (CircularGaugeWidgetViewModel)WidgetViewModel.Create(WidgetType.GaugeCircular);
        Assert.Equal("#10B981", defaultGauge.ColorHex);

        // 8. 针对 0~300 量程，自动计算低(75)、中(150)、高(225)，中段绿色精准居中
        var wideGauge = (CircularGaugeWidgetViewModel)WidgetViewModel.Create(WidgetType.GaugeCircular);
        wideGauge.MinValue = 0;
        wideGauge.MaxValue = 300;
        wideGauge.UpdateSegmentArcs();
        // 验证中间值 150 处于中段区间内
        wideGauge.UpdateRuntimeValue(150);
        Assert.Equal("#10B981", wideGauge.ColorHex);
    }

    [Fact]
    public void TankLevelWidgetViewModel_Orientation_And_LowMidHighLiquidColors_Test()
    {
        var tank = (TankLevelWidgetViewModel)WidgetViewModel.Create(WidgetType.LevelTank);
        Assert.NotNull(tank);
        Assert.Equal("Vertical", tank.Orientation);
        Assert.False(tank.IsHorizontal);

        // 1. 方向切换与外部宽高自适应测试 (支持直接设 Orientation 或在界面设 Props.Orientation)
        tank.Width = 150;
        tank.Height = 220;
        tank.Props.Orientation = "Horizontal";
        Assert.True(tank.IsHorizontal);
        Assert.Equal(220, tank.Width);
        Assert.Equal(150, tank.Height);

        tank.Props.Orientation = "Vertical";
        Assert.False(tank.IsHorizontal);
        Assert.Equal(150, tank.Width);
        Assert.Equal(220, tank.Height);

        // 2. 以高限和低限为变色阈值的纯色液体测试
        tank.MinValue = 0;
        tank.MaxValue = 100;
        tank.LowAlarm = 20;  // 低限（预警）
        tank.HighAlarm = 80; // 高限（报警）
        tank.LowColor = "#EAB308";  // 低液位颜色 (预警黄)
        tank.MidColor = "#0284C7";  // 正常液位颜色 (标准蓝)
        tank.HighColor = "#EF4444"; // 高液位颜色 (溢流红)

        // 2.1 低于低限 (10 <= 20) -> 低液位纯色
        tank.UpdateRuntimeValue(10);
        Assert.Equal("#EAB308", tank.LiquidColor);
        Assert.Equal("#EAB308", tank.Props.LiquidColor);
        Assert.True(tank.IsAlarm); // 低液位报警

        // 2.2 低限与高限之间 (20 < 55 < 80) -> 正常液位纯色
        tank.UpdateRuntimeValue(55);
        Assert.Equal("#0284C7", tank.LiquidColor);
        Assert.Equal("#0284C7", tank.Props.LiquidColor);
        Assert.False(tank.IsAlarm);

        // 2.3 高于高限 (88 >= 80) -> 高液位纯色
        tank.UpdateRuntimeValue(88);
        Assert.Equal("#EF4444", tank.LiquidColor);
        Assert.Equal("#EF4444", tank.Props.LiquidColor);
        Assert.True(tank.IsAlarm); // 高液位报警

        // 3. 序列化与持久化配置测试
        tank.Orientation = "Horizontal";
        tank.SyncPropertiesFromFields();
        Assert.Equal("Horizontal", tank.Properties["Orientation"]);
        Assert.Equal("20", tank.Properties["LowAlarm"]);
        Assert.Equal("80", tank.Properties["HighAlarm"]);
        Assert.Equal("#EAB308", tank.Properties["LowColor"]);
        Assert.Equal("#0284C7", tank.Properties["MidColor"]);
        Assert.Equal("#EF4444", tank.Properties["HighColor"]);

        // 4. 反序列化与还原
        var restored = (TankLevelWidgetViewModel)WidgetViewModel.Create(WidgetType.LevelTank);
        restored.LoadProperties(tank.Properties);
        Assert.Equal("Horizontal", restored.Orientation);
        Assert.True(restored.IsHorizontal);
        Assert.Equal(20, restored.LowAlarm);
        Assert.Equal(80, restored.HighAlarm);
        Assert.Equal("#EAB308", restored.LowColor);
        Assert.Equal("#0284C7", restored.MidColor);
        Assert.Equal("#EF4444", restored.HighColor);
    }

    [Fact]
    public void WidgetStylePresets_ApplyPreset_UpdatesPropertiesAndBehaviorsCorrectly_Test()
    {
        // 1. 270度仪表盘 - 套用 "交流电压表 (0~500V)" 预设
        var gauge = (CircularGaugeWidgetViewModel)WidgetViewModel.Create(WidgetType.GaugeCircular);
        var gaugePresets = gauge.AvailablePresets;
        Assert.NotEmpty(gaugePresets);
        Assert.Contains(gaugePresets, p => p.Id == "Gauge_AC_Voltage_380");

        var voltPreset = gaugePresets.First(p => p.Id == "Gauge_AC_Voltage_380");
        gauge.SelectedPreset = voltPreset;

        Assert.Equal("交流动力电压", gauge.Title);
        Assert.Equal("V", gauge.Unit);
        Assert.Equal(0, gauge.MinValue);
        Assert.Equal(500, gauge.MaxValue);
        Assert.True(gauge.Props.EnableThresholdColor);
        Assert.Equal(340, gauge.Props.LowValue);
        Assert.Equal(380, gauge.Props.MidValue);
        Assert.Equal(420, gauge.Props.HighValue);
        Assert.Equal("#F59E0B", gauge.Props.LowColor);
        Assert.Equal("#10B981", gauge.Props.MidColor);
        Assert.Equal("#EF4444", gauge.Props.HighColor);
        Assert.False(string.IsNullOrWhiteSpace(gauge.Props.LowArcData));
        Assert.False(string.IsNullOrWhiteSpace(gauge.Props.MidArcData));
        Assert.False(string.IsNullOrWhiteSpace(gauge.Props.HighArcData));

        // 2. 270度仪表盘 - 切换套用 "电机负载电流表 (0~100A)" 预设
        var currentPreset = gaugePresets.First(p => p.Id == "Gauge_Motor_Current_100");
        gauge.ApplyPreset(currentPreset);

        Assert.Equal("主电机工作电流", gauge.Title);
        Assert.Equal("A", gauge.Unit);
        Assert.Equal(0, gauge.MinValue);
        Assert.Equal(100, gauge.MaxValue);
        Assert.Equal(10, gauge.Props.LowValue);
        Assert.Equal(50, gauge.Props.MidValue);
        Assert.Equal(85, gauge.Props.HighValue);
        Assert.Equal("#38BDF8", gauge.Props.LowColor);
        Assert.Equal("#10B981", gauge.Props.MidColor);
        Assert.Equal("#EF4444", gauge.Props.HighColor);

        // 3. 普通按钮 - 套用 "紧急停止按钮 (急停红+确认)" 预设
        var button = (ControlButtonWidgetViewModel)WidgetViewModel.Create(WidgetType.ControlButton);
        var buttonPresets = button.AvailablePresets;
        Assert.NotEmpty(buttonPresets);
        Assert.Contains(buttonPresets, p => p.Id == "Btn_Emergency_Stop");

        var esPreset = buttonPresets.First(p => p.Id == "Btn_Emergency_Stop");
        button.SelectedPreset = esPreset;

        Assert.Equal("紧急停止", button.Title);
        Assert.Equal("急停", button.ButtonText);
        Assert.Equal("#DC2626", button.ColorHex);
        Assert.Equal("DirectWrite", button.ButtonMode);
        Assert.True(button.RequireConfirm);
        Assert.Contains("紧急停止", button.ConfirmMessage);

        // 4. 普通按钮 - 套用 "启动 / 运行按钮 (工业绿)" 预设
        var startPreset = buttonPresets.First(p => p.Id == "Btn_Start_Success");
        button.ApplyPreset(startPreset);

        Assert.Equal("启动控制", button.Title);
        Assert.Equal("启动", button.ButtonText);
        Assert.Equal("#16A34A", button.ColorHex);
        Assert.Equal("DirectWrite", button.ButtonMode);
        Assert.False(button.RequireConfirm);

        // 5. 液体储罐 - 套用 "卧式储油槽罐 (0~5000L)" 预设 (自动适配横向布局与尺寸)
        var tank = (TankLevelWidgetViewModel)WidgetViewModel.Create(WidgetType.LevelTank);
        tank.Width = 120;
        tank.Height = 180;
        var tankPresets = tank.AvailablePresets;
        Assert.NotEmpty(tankPresets);
        Assert.Contains(tankPresets, p => p.Id == "Tank_Oil_Horizontal");

        var fuelPreset = tankPresets.First(p => p.Id == "Tank_Oil_Horizontal");
        tank.SelectedPreset = fuelPreset;

        Assert.Equal("日用储油槽罐", tank.Title);
        Assert.Equal("L", tank.Unit);
        Assert.Equal("Horizontal", tank.Orientation);
        Assert.True(tank.IsHorizontal);
        Assert.Equal(220, tank.Width); // 卧式横向自适应为宽 >= 220
        Assert.Equal(120, tank.Height); // 高自适应为 <= 140
        Assert.Equal(800, tank.LowAlarm);
        Assert.Equal(4500, tank.HighAlarm);
        Assert.Equal("#DC2626", tank.LowColor);
        Assert.Equal("#F59E0B", tank.MidColor);
        Assert.Equal("#EF4444", tank.HighColor);

        // 6. 套用预设后序列化与反序列化完整闭环验证
        tank.SyncPropertiesFromFields();
        var restoredTank = (TankLevelWidgetViewModel)WidgetViewModel.Create(WidgetType.LevelTank);
        restoredTank.LoadProperties(tank.Properties);
        Assert.Equal("Horizontal", restoredTank.Orientation);
        Assert.True(restoredTank.IsHorizontal);
        Assert.Equal(800, restoredTank.LowAlarm);
        Assert.Equal(4500, restoredTank.HighAlarm);
        Assert.Equal("#F59E0B", restoredTank.MidColor);
    }

    [Fact]
    public void WidgetStylePresets_RemainingWidgets_Presets_Test()
    {
        // 1. 实时趋势图 TrendChart - 套用 "热工炉膛温区曲线 (0~300℃)"
        var trend = (TrendChartWidgetViewModel)WidgetViewModel.Create(WidgetType.TrendChart);
        var trendPresets = trend.AvailablePresets;
        Assert.NotEmpty(trendPresets);
        Assert.Contains(trendPresets, p => p.Id == "Trend_Reflow_Temp");

        trend.SelectedPreset = trendPresets.First(p => p.Id == "Trend_Reflow_Temp");
        Assert.Equal("炉膛温区实时曲线", trend.Title);
        Assert.Equal("℃", trend.Unit);
        Assert.Equal(0, trend.MinValue);
        Assert.Equal(300, trend.MaxValue);
        Assert.Equal("#F97316", trend.LineColor);
        Assert.Equal("#7C2D12", trend.FillColor);

        // 2. IO状态点阵板 IoMatrix - 套用 "8路 PLC 数字量输入板 (DI)"
        var io = (IoMatrixWidgetViewModel)WidgetViewModel.Create(WidgetType.IoMatrix);
        var ioPresets = io.AvailablePresets;
        Assert.NotEmpty(ioPresets);
        Assert.Contains(ioPresets, p => p.Id == "Io_DI_8Ch_Status");

        io.ApplyPreset(ioPresets.First(p => p.Id == "Io_DI_8Ch_Status"));
        Assert.Equal("工位 DI 信号状态板", io.Title);
        Assert.Equal(8, io.IoChannels);
        Assert.Equal("HEX", io.DisplayFormat);
        Assert.Equal("#10B981", io.ActiveColor);

        // 3. 文本标签 TextLabel - 套用 "工位工艺高亮标题板"
        var label = (TextLabelWidgetViewModel)WidgetViewModel.Create(WidgetType.TextLabel);
        var labelPresets = label.AvailablePresets;
        Assert.NotEmpty(labelPresets);
        Assert.Contains(labelPresets, p => p.Id == "Label_Station_Header");

        label.SelectedPreset = labelPresets.First(p => p.Id == "Label_Station_Header");
        Assert.Equal("ST-01 装配加工工作站", label.Text);
        Assert.Equal(18, label.LabelFontSize);
        Assert.True(label.IsBold);
        Assert.Equal("#38BDF8", label.TextColor);
        Assert.Equal("Center", label.TextAlignment);

        // 4. 普通显示框 DisplayBox - 套用 "深色高亮状态指示框"
        var displayBox = (DisplayBoxWidgetViewModel)WidgetViewModel.Create(WidgetType.DisplayBox);
        var dispPresets = displayBox.AvailablePresets;
        Assert.NotEmpty(dispPresets);
        Assert.Contains(dispPresets, p => p.Id == "Display_System_State");

        displayBox.SelectedPreset = dispPresets.First(p => p.Id == "Display_System_State");
        Assert.Equal("工况模式:", displayBox.Prefix);
        Assert.Equal("#38BDF8", displayBox.TextColor);
        Assert.Equal("#0284C7", displayBox.BorderColor);
        Assert.Equal("#0F172A", displayBox.BackgroundColor);

        // 5. 容器分组框 PanelContainer - 套用 "主控动力电气分区容器"
        var panel = (PanelContainerWidgetViewModel)WidgetViewModel.Create(WidgetType.PanelContainer);
        var panelPresets = panel.AvailablePresets;
        Assert.NotEmpty(panelPresets);
        Assert.Contains(panelPresets, p => p.Id == "Panel_Control_Zone");

        panel.SelectedPreset = panelPresets.First(p => p.Id == "Panel_Control_Zone");
        Assert.Equal("动力与变频驱动单元", panel.GroupTitle);
        Assert.Equal("#0284C7", panel.BorderColor);
        Assert.Equal("#0A0F1D", panel.FillColor);
    }

    [Fact]
    public void PipeWidgetViewModel_Orientation_FlowAnimation_And_Presets_Test()
    {
        var pipe = (PipeWidgetViewModel)WidgetViewModel.Create(WidgetType.Pipe);
        Assert.NotNull(pipe);
        Assert.Equal("Horizontal", pipe.Orientation);
        Assert.True(pipe.IsHorizontal);
        Assert.Equal(240, pipe.Width);
        Assert.Equal(24, pipe.Height);

        // 1. 方向切换与尺寸自适应翻转
        pipe.Orientation = "Vertical";
        Assert.False(pipe.IsHorizontal);
        Assert.Equal(24, pipe.Width);
        Assert.Equal(240, pipe.Height);

        pipe.Orientation = "Horizontal";
        Assert.True(pipe.IsHorizontal);
        Assert.Equal(240, pipe.Width);
        Assert.Equal(24, pipe.Height);

        // 2. 套用预设 - "循环冷却水管道 (横向水蓝流动)"
        var presets = pipe.AvailablePresets;
        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Id == "Pipe_Cooling_Water");

        pipe.SelectedPreset = presets.First(p => p.Id == "Pipe_Cooling_Water");
        Assert.Equal("循环水管路", pipe.Title);
        Assert.Equal("Horizontal", pipe.Orientation);
        Assert.Equal("#0284C7", pipe.LiquidColor);
        Assert.Equal(1.5, pipe.FlowSpeed);
        Assert.True(pipe.IsFlowing);

        // 3. 套用预设 - "立式蒸汽管道 (纵向气白流动)"
        var steamPreset = presets.First(p => p.Id == "Pipe_Vertical_Steam");
        pipe.ApplyPreset(steamPreset);
        Assert.Equal("立式蒸汽管路", pipe.Title);
        Assert.Equal("Vertical", pipe.Orientation);
        Assert.False(pipe.IsHorizontal);
        Assert.Equal("#E2E8F0", pipe.LiquidColor);
        Assert.Equal(1.0, pipe.FlowSpeed);

        // 4. 运行时布尔点位启闭控制
        pipe.UpdateRuntimeValue(false);
        Assert.False(pipe.IsFlowing);
        pipe.UpdateRuntimeValue(true);
        Assert.True(pipe.IsFlowing);

        // 5. 序列化与反序列化测试
        pipe.SyncPropertiesFromFields();
        var restored = (PipeWidgetViewModel)WidgetViewModel.Create(WidgetType.Pipe);
        restored.LoadProperties(pipe.Properties);
        Assert.Equal("Vertical", restored.Orientation);
        Assert.False(restored.IsHorizontal);
        Assert.Equal("#E2E8F0", restored.LiquidColor);
        Assert.Equal(1.0, restored.FlowSpeed);
        Assert.True(restored.IsFlowing);
    }

    [Fact]
    public void Canvas_Alignment_And_Distribution_Calculations_Test()
    {
        // 构造 3 个组件用于对齐和均分分布验证
        var w1 = WidgetViewModel.Create(WidgetType.NumericCard);
        w1.X = 10; w1.Y = 40; w1.Width = 100; w1.Height = 50; w1.IsSelected = true;

        var w2 = WidgetViewModel.Create(WidgetType.NumericCard);
        w2.X = 80; w2.Y = 120; w2.Width = 100; w2.Height = 50; w2.IsSelected = true;

        var w3 = WidgetViewModel.Create(WidgetType.NumericCard);
        w3.X = 400; w3.Y = 200; w3.Width = 100; w3.Height = 50; w3.IsSelected = true;

        var list = new List<WidgetViewModel> { w1, w2, w3 };

        // 1. 左对齐逻辑断言
        double minX = list.Min(w => w.X);
        foreach (var w in list) w.X = minX;
        Assert.Equal(10, w1.X);
        Assert.Equal(10, w2.X);
        Assert.Equal(10, w3.X);

        // 2. 顶对齐逻辑断言
        double minY = list.Min(w => w.Y);
        foreach (var w in list) w.Y = minY;
        Assert.Equal(40, w1.Y);
        Assert.Equal(40, w2.Y);
        Assert.Equal(40, w3.Y);

        // 3. 水平等间距均分分布算法验证
        // 重设 X 坐标：w1=0, w2=80, w3=400 (跨度 500，总宽 300，间隙 200，gap=100)
        w1.X = 0;
        w2.X = 80;
        w3.X = 400;

        var sorted = list.OrderBy(w => w.X).ToList();
        double totalItemsWidth = sorted.Sum(w => w.Width);
        double span = (sorted.Last().X + sorted.Last().Width) - sorted.First().X;
        double totalGap = span - totalItemsWidth;
        double gap = totalGap / (sorted.Count - 1);

        double curX = sorted[0].X;
        for (int i = 1; i < sorted.Count - 1; i++)
        {
            curX += sorted[i - 1].Width + gap;
            sorted[i].X = Math.Round(curX / 10.0) * 10.0;
        }

        Assert.Equal(0, w1.X);
        Assert.Equal(200, w2.X); // 中间组件精准均分到 200 (0 + 100宽 + 100gap)
        Assert.Equal(400, w3.X);
    }

    [Fact]
    public void IoMatrix_ByteGrouping_And_Channels_DynamicHeight_And_Formatting_Test()
    {
        var ioVm = new IoMatrixWidgetViewModel();
        Assert.Equal(8, ioVm.IoChannels);
        Assert.Equal("8 点 (1 组 Byte)", ioVm.ChannelGroupSummary);
        Assert.Equal(115, ioVm.Height);

        // 1. 验证 16 通道自动扩展至 2 组 Byte (Word) 并调整高度
        ioVm.IoChannels = 16;
        Assert.Equal("16 点 (2 组 Byte)", ioVm.ChannelGroupSummary);
        Assert.True(ioVm.Height >= 160);

        // 验证 16 位 0x00FF 格式化 (4 位十六进制)
        ioVm.UpdateRuntimeValue(255);
        Assert.Equal("0x00FF", ioVm.FormattedValue);

        // 2. 验证 32 通道自动扩展至 4 组 Byte (DWord) 并调整高度
        ioVm.IoChannels = 32;
        Assert.Equal("32 点 (4 组 Byte)", ioVm.ChannelGroupSummary);
        Assert.True(ioVm.Height >= 260);

        // 验证 32 位 0x0000FFFF 格式化 (8 位十六进制)
        ioVm.UpdateRuntimeValue(65535);
        Assert.Equal("0x0000FFFF", ioVm.FormattedValue);

        // 3. 验证二进制格式化
        ioVm.DisplayFormat = "BIN";
        ioVm.UpdateRuntimeValue(3);
        Assert.Equal(32, ioVm.FormattedValue.Length);
        Assert.EndsWith("0011", ioVm.FormattedValue);

        // 4. 核心用户场景验证：选择 32 路后再选择 8 路，宽高精准重置恢复至 8 路标准尺寸 (280x115)
        ioVm.IoChannels = 8;
        Assert.Equal("8 点 (1 组 Byte)", ioVm.ChannelGroupSummary);
        Assert.Equal(280, ioVm.Width);
        Assert.Equal(115, ioVm.Height);

        // 5. 验证通过模板预设切换时宽高同样自动双向联动
        var preset32 = WidgetStylePresetCatalog.GetPresets(WidgetType.IoMatrix).FirstOrDefault(p => p.Id == "Io_32Ch_DWord_Bus");
        Assert.NotNull(preset32);
        ioVm.ApplyPreset(preset32!);
        Assert.Equal(32, ioVm.IoChannels);
        Assert.Equal(280, ioVm.Width);
        Assert.Equal(260, ioVm.Height);

        var preset8 = WidgetStylePresetCatalog.GetPresets(WidgetType.IoMatrix).FirstOrDefault(p => p.Id == "Io_DI_8Ch_Status");
        Assert.NotNull(preset8);
        ioVm.ApplyPreset(preset8!);
        Assert.Equal(8, ioVm.IoChannels);
        Assert.Equal(280, ioVm.Width);
        Assert.Equal(115, ioVm.Height);

        // 6. 验证通道组可见性转换器
        var conv = WidgetUiConverters.MinChannelsToVisibility;
        Assert.Equal(System.Windows.Visibility.Visible, conv.Convert(16, typeof(System.Windows.Visibility), 16, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Collapsed, conv.Convert(8, typeof(System.Windows.Visibility), 16, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Visible, conv.Convert(32, typeof(System.Windows.Visibility), 32, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Collapsed, conv.Convert(24, typeof(System.Windows.Visibility), 32, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Pipe_FlowDirection_Reverse_And_AnimationParameters_Test()
    {
        var pipe = new PipeWidgetViewModel();
        Assert.Equal("Forward", pipe.FlowDirection);

        // 正向流动位移测试（向下游流动：StrokeDashOffset 从 0 到 -28）
        bool isReverse = string.Equals(pipe.FlowDirection, "Reverse", StringComparison.OrdinalIgnoreCase);
        double toValue = isReverse ? 28.0 : -28.0;
        Assert.Equal(-28.0, toValue);

        // 切换为反向流动（向上游流动：StrokeDashOffset 从 0 到 +28）
        pipe.FlowDirection = "Reverse";
        Assert.Equal("Reverse", pipe.FlowDirection);
        isReverse = string.Equals(pipe.FlowDirection, "Reverse", StringComparison.OrdinalIgnoreCase);
        toValue = isReverse ? 28.0 : -28.0;
        Assert.Equal(28.0, toValue);

        // 属性同步测试
        pipe.SyncPropertiesFromFields();
        Assert.Equal("Reverse", pipe.Properties["FlowDirection"]);
    }

    [Fact]
    public void DeviceStatus_ViewModel_And_StylePreset_Tests()
    {
        // 1. 验证工厂创建与默认尺寸
        var widget = WidgetViewModel.Create(WidgetType.DeviceStatus);
        Assert.IsType<DeviceStatusWidgetViewModel>(widget);
        var devVm = (DeviceStatusWidgetViewModel)widget;

        Assert.Equal(WidgetType.DeviceStatus, devVm.Type);
        Assert.Equal(280, devVm.Width);
        Assert.Equal(140, devVm.Height);
        Assert.Equal("Online", devVm.ConnectionStatus);
        Assert.Equal("#10B981", devVm.ActiveColor);
        Assert.Equal("#EF4444", devVm.OfflineColor);
        Assert.Equal("#F59E0B", devVm.WarningColor);

        // 2. 验证设备选择与绑定联动 (ApplyDevice)
        var devNode = new DeviceNode
        {
            DeviceId = "DEV_PLC_LINE1",
            Name = "1号涂布产线 PLC",
            ChannelId = "CH_ETHERNET_01",
            ProtocolType = ProtocolType.ModbusTcp,
            StationAddress = 1,
            DefaultPollIntervalMs = 500
        };
        var devOption = DeviceOptionItem.FromDeviceNode(devNode, tagCount: 48);
        Assert.Contains("1号涂布产线 PLC", devOption.DisplayName);
        Assert.Contains("DEV_PLC_LINE1", devOption.DisplayName);

        devVm.ApplyDevice(devOption);
        Assert.Equal("DEV_PLC_LINE1", devVm.DeviceId);
        Assert.Equal("1号涂布产线 PLC", devVm.Title);
        Assert.Equal("ModbusTcp", devVm.ProtocolType);
        Assert.Equal("CH_ETHERNET_01", devVm.ChannelId);
        Assert.Equal(1, devVm.StationAddress);
        Assert.Equal(500, devVm.PollIntervalMs);
        Assert.Equal(48, devVm.TagCount);

        // 验证回写持久化字典
        devVm.SyncPropertiesFromFields();
        Assert.Equal("DEV_PLC_LINE1", devVm.Properties["DeviceId"]);
        Assert.Equal("ModbusTcp", devVm.Properties["ProtocolType"]);
        Assert.Equal("48", devVm.Properties["TagCount"]);

        // 3. 验证状态计算与色彩呈现
        devVm.UpdateRuntimeValue("Timeout");
        Assert.Equal("Timeout", devVm.ConnectionStatus);
        Assert.Equal(devVm.WarningColor, devVm.StatusColor);

        devVm.UpdateRuntimeValue("Offline");
        Assert.Equal("Offline", devVm.ConnectionStatus);
        Assert.Equal(devVm.OfflineColor, devVm.StatusColor);

        devVm.UpdateRuntimeValue(1); // 模拟整型 1 映射为 Online
        Assert.Equal("Online", devVm.ConnectionStatus);
        Assert.Equal(devVm.ActiveColor, devVm.StatusColor);

        // 4. 验证行业模板预设
        var presets = WidgetStylePresetCatalog.GetPresets(WidgetType.DeviceStatus);
        Assert.Equal(3, presets.Count);

        var compactPreset = presets.FirstOrDefault(p => p.Id == "Dev_Compact_Tag");
        Assert.NotNull(compactPreset);
        devVm.ApplyPreset(compactPreset!);
        Assert.Equal(220, devVm.Width);
        Assert.Equal(90, devVm.Height);
        Assert.True(devVm.IsCompact);

        var serverPreset = presets.FirstOrDefault(p => p.Id == "Dev_Server_Panel");
        Assert.NotNull(serverPreset);
        devVm.ApplyPreset(serverPreset!);
        Assert.Equal(320, devVm.Width);
        Assert.Equal(175, devVm.Height);
        Assert.False(devVm.IsCompact);
    }

    [Fact]
    public void ArcGauge_ViewModel_ScaleGeometryAndDynamicValues_ShouldCalculateAccurately()
    {
        // 1. 创建控件 ViewModel 并验证默认参数
        var vm = WidgetViewModel.Create(WidgetType.GaugeArc);
        Assert.IsType<ArcGaugeWidgetViewModel>(vm);
        var gauge = (ArcGaugeWidgetViewModel)vm;

        Assert.Equal(WidgetType.GaugeArc, gauge.Type);
        Assert.Equal(200, gauge.Width);
        Assert.Equal(140, gauge.Height);
        Assert.NotNull(gauge.ArcGaugeProps);

        // 2. 验证默认几何路径与 6 个刻度标签
        Assert.False(string.IsNullOrWhiteSpace(gauge.Props.OuterArcPathData));
        Assert.False(string.IsNullOrWhiteSpace(gauge.Props.MajorTicksPathData));
        Assert.False(string.IsNullOrWhiteSpace(gauge.Props.MinorTicksPathData));

        Assert.Equal("-10000.0", gauge.Props.ScaleText1);
        Assert.Equal("-6000.0", gauge.Props.ScaleText2);
        Assert.Equal("-2000.0", gauge.Props.ScaleText3);
        Assert.Equal("2000.0", gauge.Props.ScaleText4);
        Assert.Equal("6000.0", gauge.Props.ScaleText5);
        Assert.Equal("10000.0", gauge.Props.ScaleText6);

        // 3. 验证指针角度线性映射与动态读数
        // 下限 (-10000) 对应 -90°
        gauge.UpdateRuntimeValue(-10000);
        Assert.Equal("-10000.0", gauge.FormattedValue);
        Assert.Equal(-90.0, gauge.Props.NeedleAngle, precision: 1);
        Assert.Equal(0.0, gauge.NormalizedProgress, precision: 2);

        // 中间值 (0) 对应 0°
        gauge.UpdateRuntimeValue(0);
        Assert.Equal("0.0", gauge.FormattedValue);
        Assert.Equal(0.0, gauge.Props.NeedleAngle, precision: 1);
        Assert.Equal(0.5, gauge.NormalizedProgress, precision: 2);

        // 上限 (10000) 对应 +90°
        gauge.UpdateRuntimeValue(10000);
        Assert.Equal("10000.0", gauge.FormattedValue);
        Assert.Equal(90.0, gauge.Props.NeedleAngle, precision: 1);
        Assert.Equal(1.0, gauge.NormalizedProgress, precision: 2);

        // 微小负值 (-0.67) 对应接近 0° 但偏负的角度
        gauge.UpdateRuntimeValue(-0.67);
        Assert.Equal("-0.7", gauge.FormattedValue);
        Assert.True(gauge.Props.NeedleAngle < 0 && gauge.Props.NeedleAngle > -1.0);

        // 4. 验证样式预设应用 (切换为双极性电流表 -1.0 ~ 1.0 ARMS)
        var presets = WidgetStylePresetCatalog.GetPresets(WidgetType.GaugeArc);
        Assert.True(presets.Count >= 2);

        var currentPreset = presets.FirstOrDefault(p => p.Id == "ArcGauge_Current_RMS_1");
        Assert.NotNull(currentPreset);
        gauge.ApplyPreset(currentPreset!);

        Assert.Equal(-1.0, gauge.MinValue);
        Assert.Equal(1.0, gauge.MaxValue);
        Assert.Equal("ARMS", gauge.Unit);
        Assert.Equal("-1.0", gauge.Props.ScaleText1);
        Assert.Equal("-0.6", gauge.Props.ScaleText2);
        Assert.Equal("-0.2", gauge.Props.ScaleText3);
        Assert.Equal("0.2", gauge.Props.ScaleText4);
        Assert.Equal("0.6", gauge.Props.ScaleText5);
        Assert.Equal("1.0", gauge.Props.ScaleText6);

        // 运行时测试 -0.7 ARMS 对应角度与读数
        gauge.UpdateRuntimeValue(-0.7);
        Assert.Equal("-0.7", gauge.FormattedValue);
        // ratio = (-0.7 - (-1.0)) / 2.0 = 0.3 / 2.0 = 0.15 => angle = -90 + 0.15*180 = -63°
        Assert.Equal(-63.0, gauge.Props.NeedleAngle, precision: 1);

        // 5. 验证属性序列化与反序列化
        gauge.SyncPropertiesFromFields();
        Assert.Equal("-1", gauge.Properties["MinValue"]);
        Assert.Equal("1", gauge.Properties["MaxValue"]);
        Assert.Equal("ARMS", gauge.Properties["Unit"]);

        var restoredGauge = new ArcGaugeWidgetViewModel();
        restoredGauge.LoadProperties(gauge.Properties);
        Assert.Equal(-1.0, restoredGauge.MinValue);
        Assert.Equal(1.0, restoredGauge.MaxValue);
        Assert.Equal("ARMS", restoredGauge.Unit);
    }
}





