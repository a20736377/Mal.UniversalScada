using System;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.UI.Controls.Metadata;
using Mal.UniversalScada.UI.Controls.PropertyEditors;
using Mal.UniversalScada.UI.Controls.ViewModels;
using Mal.UniversalScada.UI.Controls.Widgets;
using Xunit;

namespace Mal.UniversalScada.Core.Tests;

public class WidgetRegistryAndDecouplingTests
{
    private static void RunInSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    [Fact]
    public void BuiltInWidgets_AreDiscoveredAndRegistered()
    {
        var registry = WidgetRegistry.Instance;
        var allWidgets = registry.GetAllWidgets().ToList();

        // 验证系统内置组件均已被自动发现并注册 (16种标准工控组件)
        Assert.True(allWidgets.Count >= 16);

        // 验证核心组件均可检索到
        var circularDesc = registry.GetDescriptor(WidgetType.GaugeCircular);
        Assert.NotNull(circularDesc);
        Assert.Equal("圆形仪表盘", circularDesc.DisplayName);
        Assert.Equal("仪表类", circularDesc.Category);
        Assert.NotNull(circularDesc.ViewType);
        Assert.NotNull(circularDesc.PropertyEditorType);

        var numericDesc = registry.GetDescriptor(WidgetType.NumericCard);
        Assert.NotNull(numericDesc);
        Assert.Equal("数显卡片", numericDesc.DisplayName);

        var deviceDesc = registry.GetDescriptor(WidgetType.DeviceStatus);
        Assert.NotNull(deviceDesc);
        Assert.Equal("设备状态卡", deviceDesc.DisplayName);

        var labelDesc = registry.GetDescriptor(WidgetType.TextLabel);
        Assert.NotNull(labelDesc);
        Assert.Equal("文本标签", labelDesc.DisplayName);

        // 验证按类别分组
        var categories = registry.GetCategories().ToList();
        Assert.Contains("仪表类", categories);
        Assert.Contains("工业图元", categories);
        Assert.Contains("基础图元", categories);
        Assert.Contains("控制交互", categories);
    }

    [Fact]
    public void FactoryMethods_CreateCorrectViewModels()
    {
        var vmGauge = WidgetRegistry.Instance.CreateViewModel(WidgetType.GaugeCircular);
        Assert.IsType<CircularGaugeWidgetViewModel>(vmGauge);
        Assert.Equal(WidgetType.GaugeCircular, vmGauge.Type);

        var vmCard = WidgetViewModel.Create(WidgetType.NumericCard);
        Assert.IsType<NumericCardWidgetViewModel>(vmCard);

        var vmPipe = WidgetViewModel.Create(WidgetType.Pipe);
        Assert.IsType<PipeWidgetViewModel>(vmPipe);

        // 验证默认尺寸从注册表获取
        Assert.Equal(180, WidgetViewModel.GetDefaultWidth(WidgetType.GaugeCircular));
        Assert.Equal(180, WidgetViewModel.GetDefaultHeight(WidgetType.GaugeCircular));
    }

    [Fact]
    public void ExternalComponentLibrary_RegistrationAndDynamicDiscovery()
    {
        RunInSta(() =>
        {
            var registry = WidgetRegistry.Instance;
            bool eventFired = false;
            EventHandler handler = (s, e) => eventFired = true;
            registry.RegistryChanged += handler;

            try
            {
                // 模拟外部第三方控件库注册自定义组件
                var customDesc = new WidgetDescriptor(
                    typeId: "Acme.SmartSensor",
                    displayName: "Acme 智能振动传感器",
                    viewModelType: typeof(MockSmartSensorWidgetViewModel),
                    category: "第三方扩展库",
                    icon: "📡",
                    description: "工业高频无线振动传感器监控组件",
                    defaultWidth: 240,
                    defaultHeight: 160,
                    defaultTitle: "1#振动传感器",
                    order: 100,
                    viewType: typeof(MockSmartSensorView),
                    propertyEditorType: typeof(MockSmartSensorPropertyEditor),
                    type: WidgetType.Custom);

                registry.RegisterWidget(customDesc);

                Assert.True(eventFired);

                // 验证按 typeId 查询
                var retrieved = registry.GetDescriptor("Acme.SmartSensor");
                Assert.NotNull(retrieved);
                Assert.Equal("Acme 智能振动传感器", retrieved.DisplayName);
                Assert.Equal("第三方扩展库", retrieved.Category);

                // 验证通过工厂创建 ViewModel 实例
                var vm = registry.CreateViewModel("Acme.SmartSensor");
                Assert.NotNull(vm);
                Assert.IsType<MockSmartSensorWidgetViewModel>(vm);
                Assert.Equal(WidgetType.Custom, vm.Type);
                Assert.Equal("Acme.SmartSensor", vm.CustomTypeName);

                // 验证配置序列化与反序列化
                var cfg = vm.ToConfig();
                Assert.Equal(WidgetType.Custom, cfg.Type);
                Assert.Equal("Acme.SmartSensor", cfg.CustomTypeName);

                var recreatedVm = WidgetViewModel.FromConfig(cfg);
                Assert.NotNull(recreatedVm);
                Assert.IsType<MockSmartSensorWidgetViewModel>(recreatedVm);
                Assert.Equal("Acme.SmartSensor", recreatedVm.CustomTypeName);

                // 验证 View 模板选择器能够无反射硬编码动态识别第三方组件 View
                var viewTemplate = WidgetViewTemplateSelector.Instance.SelectTemplate(vm, new ContentPresenter());
                Assert.NotNull(viewTemplate);
                var viewVisual = viewTemplate.LoadContent();
                Assert.IsType<MockSmartSensorView>(viewVisual);

                // 验证 属性编辑模板选择器能够无硬编码动态识别第三方组件属性面板
                var editorTemplate = WidgetEditorTemplateSelector.Instance.SelectTemplate(vm, new ContentPresenter());
                Assert.NotNull(editorTemplate);
                var editorVisual = editorTemplate.LoadContent();
                Assert.IsType<MockSmartSensorPropertyEditor>(editorVisual);
            }
            finally
            {
                registry.RegistryChanged -= handler;
            }
        });
    }

    [Fact]
    public void PackageContract_InitializesCorrectly()
    {
        var package = new MockWidgetPackage();
        WidgetRegistry.Instance.RegisterPackage(package);

        var desc = WidgetRegistry.Instance.GetDescriptor("MockPackage.FlowController");
        Assert.NotNull(desc);
        Assert.Equal("智能流量控制器", desc.DisplayName);
    }
}

// 模拟外部第三方扩展组件库中的类定义
[ScadaWidget(
    "Acme.SmartSensor",
    "Acme 智能振动传感器",
    Icon = "📡",
    Category = "第三方扩展库",
    Description = "工业高频无线振动传感器监控组件",
    DefaultWidth = 240,
    DefaultHeight = 160,
    DefaultTitle = "1#振动传感器",
    ViewType = typeof(MockSmartSensorView),
    PropertyEditorType = typeof(MockSmartSensorPropertyEditor))]
public class MockSmartSensorWidgetViewModel : WidgetViewModel
{
    public MockSmartSensorWidgetViewModel()
    {
        Type = WidgetType.Custom;
        CustomTypeName = "Acme.SmartSensor";
        Title = "1#振动传感器";
        Width = 240;
        Height = 160;
    }
}

public class MockSmartSensorView : UserControl
{
    public MockSmartSensorView()
    {
        Content = new TextBlock { Text = "Smart Sensor View" };
    }
}

public class MockSmartSensorPropertyEditor : UserControl
{
    public MockSmartSensorPropertyEditor()
    {
        Content = new TextBlock { Text = "Smart Sensor Editor" };
    }
}

// 模拟第三方组件包
public class MockWidgetPackage : IScadaWidgetPackage
{
    public string PackageName => "Acme Industrial Widgets Package";
    public string Version => "1.0.0";

    public void Initialize(WidgetRegistry registry)
    {
        registry.RegisterWidget(new WidgetDescriptor(
            typeId: "MockPackage.FlowController",
            displayName: "智能流量控制器",
            viewModelType: typeof(MockSmartSensorWidgetViewModel),
            category: "第三方扩展库",
            icon: "🎛️",
            description: "流量闭环控制器",
            defaultWidth: 200,
            defaultHeight: 180,
            defaultTitle: "流量控制器",
            order: 101,
            viewType: typeof(MockSmartSensorView),
            propertyEditorType: typeof(MockSmartSensorPropertyEditor),
            type: WidgetType.Custom));
    }
}
