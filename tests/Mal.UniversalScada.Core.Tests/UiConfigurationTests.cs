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
                        PrimaryTagId = "DEV_01.Temp1",
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
                        PrimaryTagId = "DEV_01.CoolantLevel",
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
                        PrimaryTagId = "DEV_01.ResetCmd",
                        X = 550,
                        Y = 200,
                        Width = 140,
                        Height = 80,
                        Action = new WidgetActionConfig
                        {
                            ActionType = "DirectWrite",
                            TargetTagId = "DEV_01.ResetCmd",
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
            PrimaryTagId = "DEV_01.Speed",
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
            TagId = "Oven_Zone1_Temp",
            Name = "1号预热区温度",
            DataType = TagDataType.Float,
            Unit = "℃",
            Address = "D1000",
            AccessMode = TagAccessMode.ReadOnly
        };

        var option = TagOptionItem.FromTagNode(tag);

        Assert.Equal("Oven_Zone1_Temp", option.TagId);
        Assert.Equal("1号预热区温度", option.Name);
        Assert.Equal(TagDataType.Float, option.DataType);
        Assert.True(option.IsNumeric);
        Assert.True(option.IsFloat);
        Assert.False(option.IsInteger);
        Assert.False(option.IsBool);
        Assert.Contains("[Float 浮点数]", option.DisplayText);
        Assert.Contains("Oven_Zone1_Temp", option.DisplayText);
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

        // 3. TextLabel (文本标签) 匹配所有类型 (亦支持不绑点位)
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.TextLabel, TagDataType.Float));
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.TextLabel, TagDataType.Bool));
        Assert.True(TagOptionItem.IsCompatibleWithWidget(WidgetType.TextLabel, TagDataType.String));

        // 4. PanelContainer (容器) 纯装饰与分组容器，不匹配任何点位
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.PanelContainer, TagDataType.Float));
        Assert.False(TagOptionItem.IsCompatibleWithWidget(WidgetType.PanelContainer, TagDataType.Bool));
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
                        PrimaryTagId = "DEV_01.Temp",
                        X = 240, Y = 20, Width = 160, Height = 60,
                        Properties = new() { ["Prefix"] = "炉温", ["Unit"] = "℃", ["Decimals"] = "1" }
                    },
                    new()
                    {
                        WidgetId = "W_CHART_1",
                        Type = WidgetType.TrendChart,
                        Title = "炉温曲线",
                        PrimaryTagId = "DEV_01.Temp",
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
        widget.PrimaryTagId = "Deleted_Tag_01";
        widget.UpdateRuntimeValue(123.45);
        Assert.Equal("Deleted_Tag_01", widget.PrimaryTagId);
        Assert.Equal(123.45, widget.CurrentRawValue);

        // 模拟点位表中该点位已被彻底删除
        var existingTags = new List<TagNode>
        {
            new() { TagId = "Valid_Tag_01", Name = "有效点位" }
        };

        // 模拟 UiDesignerViewModel 中的自动清理扫描
        if (!string.IsNullOrWhiteSpace(widget.PrimaryTagId) &&
            !existingTags.Any(t => t.TagId == widget.PrimaryTagId))
        {
            widget.PrimaryTagId = string.Empty;
            widget.UpdateRuntimeValue(null);
        }

        // 断言：点位已被置空，运行时值已被复位，避免幽灵点位
        Assert.Equal(string.Empty, widget.PrimaryTagId);
        Assert.Null(widget.CurrentRawValue);
        Assert.Equal("--", widget.FormattedValue);
    }
}

