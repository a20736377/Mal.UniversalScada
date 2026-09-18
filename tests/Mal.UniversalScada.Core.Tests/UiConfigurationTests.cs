using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
}
