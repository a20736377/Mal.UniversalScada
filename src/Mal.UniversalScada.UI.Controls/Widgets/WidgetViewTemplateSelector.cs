using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using Mal.UniversalScada.UI.Controls.Metadata;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.Widgets;

/// <summary>
/// SCADA 工业组件视图多态数据模板选择器。
/// 根据 WidgetViewModel 动态查找其注册的 XAML View 呈现控件（支持当前类库及任何外部扩展类库中的 View），
/// 彻底消除 WidgetHost.xaml 中的写死分支。
/// </summary>
public class WidgetViewTemplateSelector : DataTemplateSelector
{
    private static readonly Lazy<WidgetViewTemplateSelector> _lazy = new(() => new WidgetViewTemplateSelector());
    public static WidgetViewTemplateSelector Instance => _lazy.Value;

    private readonly ConcurrentDictionary<Type, DataTemplate> _templateCache = new();
    private readonly DataTemplate _fallbackTemplate;

    public WidgetViewTemplateSelector()
    {
        // 兜底呈现：当未配置视图时，显示基础卡片与标题
        _fallbackTemplate = new DataTemplate();
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.DarkSlateGray);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));

        var tb = new FrameworkElementFactory(typeof(TextBlock));
        tb.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Title"));
        tb.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.White);
        tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

        border.AppendChild(tb);
        _fallbackTemplate.VisualTree = border;
        _fallbackTemplate.Seal();
    }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is not WidgetViewModel vm)
        {
            return _fallbackTemplate;
        }

        var desc = WidgetRegistry.Instance.GetDescriptor(vm.GetType())
                   ?? WidgetRegistry.Instance.GetDescriptor(vm.Type);

        if (desc?.ViewType == null)
        {
            return _fallbackTemplate;
        }

        return _templateCache.GetOrAdd(desc.ViewType, CreateTemplateForType);
    }

    private static DataTemplate CreateTemplateForType(Type viewType)
    {
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(viewType);
        template.VisualTree = factory;
        template.Seal();
        return template;
    }
}
