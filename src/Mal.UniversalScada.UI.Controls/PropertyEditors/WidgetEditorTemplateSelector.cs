using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using Mal.UniversalScada.UI.Controls.Metadata;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.PropertyEditors;

/// <summary>
/// SCADA 属性编辑器多态数据模板选择器。
/// 根据选中的 WidgetViewModel 动态查找 WidgetRegistry 注册的专属属性配置面板类型，
/// 并生成对应的 DataTemplate 进行呈现，实现设计器与各组件专属面板的彻底解耦。
/// </summary>
public class WidgetEditorTemplateSelector : DataTemplateSelector
{
    private static readonly Lazy<WidgetEditorTemplateSelector> _lazy = new(() => new WidgetEditorTemplateSelector());
    public static WidgetEditorTemplateSelector Instance => _lazy.Value;

    private readonly ConcurrentDictionary<Type, DataTemplate> _templateCache = new();
    private readonly DataTemplate _emptyTemplate;

    public WidgetEditorTemplateSelector()
    {
        _emptyTemplate = new DataTemplate();
        _emptyTemplate.Seal();
    }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is not WidgetViewModel vm)
        {
            return _emptyTemplate;
        }

        var desc = WidgetRegistry.Instance.GetDescriptor(vm.GetType())
                   ?? WidgetRegistry.Instance.GetDescriptor(vm.Type);

        if (desc?.PropertyEditorType == null)
        {
            return _emptyTemplate;
        }

        return _templateCache.GetOrAdd(desc.PropertyEditorType, CreateTemplateForType);
    }

    private static DataTemplate CreateTemplateForType(Type editorType)
    {
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(editorType);
        template.VisualTree = factory;
        template.Seal();
        return template;
    }
}
