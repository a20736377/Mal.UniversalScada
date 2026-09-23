using System;
using System.Reflection;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.Metadata;

/// <summary>
/// SCADA 组件元数据描述器。
/// 封装任意类库组件的反射元数据、尺寸规范、关联 View 及属性编辑器类型。
/// </summary>
public class WidgetDescriptor
{
    /// <summary>
    /// 组件全局唯一标识（若为标准枚举则为枚举名，自定义组件为注册字符串）
    /// </summary>
    public string TypeId { get; set; } = string.Empty;

    public WidgetDescriptor() { }

    public WidgetDescriptor(
        string typeId,
        string displayName,
        Type viewModelType,
        string category = "通用组件",
        string icon = "📦",
        string description = "",
        int order = 100,
        double defaultWidth = 180,
        double defaultHeight = 140,
        string defaultTitle = "监控组件",
        Type? viewType = null,
        Type? propertyEditorType = null,
        Assembly? assembly = null,
        WidgetType type = WidgetType.Custom)
    {
        TypeId = typeId;
        DisplayName = displayName;
        ViewModelType = viewModelType;
        Category = category;
        Icon = icon;
        Description = description;
        Order = order;
        DefaultWidth = defaultWidth;
        DefaultHeight = defaultHeight;
        DefaultTitle = defaultTitle;
        ViewType = viewType;
        PropertyEditorType = propertyEditorType;
        Assembly = assembly ?? viewModelType.Assembly;
        Type = type;
    }

    /// <summary>
    /// 组件枚举类型
    /// </summary>
    public WidgetType Type { get; set; } = WidgetType.Custom;

    /// <summary>
    /// 工具箱与设计器展示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 图标
    /// </summary>
    public string Icon { get; set; } = "📦";

    /// <summary>
    /// 组件类别（"仪表类", "工业图元", "控制交互", "第三方扩展"等）
    /// </summary>
    public string Category { get; set; } = "通用组件";

    /// <summary>
    /// 组件功能描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 排序权重
    /// </summary>
    public int Order { get; set; } = 100;

    /// <summary>
    /// 默认宽度
    /// </summary>
    public double DefaultWidth { get; set; } = 180;

    /// <summary>
    /// 默认高度
    /// </summary>
    public double DefaultHeight { get; set; } = 140;

    /// <summary>
    /// 默认标题
    /// </summary>
    public string DefaultTitle { get; set; } = "监控组件";

    /// <summary>
    /// 组件 ViewModel 类型（必须派生自 WidgetViewModel）
    /// </summary>
    public Type ViewModelType { get; set; } = null!;

    /// <summary>
    /// 组件 XAML 渲染视图类型（通常为 UserControl）
    /// </summary>
    public Type? ViewType { get; set; }

    /// <summary>
    /// 组件专属属性配置编辑器类型（通常为 UserControl）
    /// </summary>
    public Type? PropertyEditorType { get; set; }

    /// <summary>
    /// 该组件所在的宿主程序集
    /// </summary>
    public Assembly Assembly { get; set; } = null!;

    /// <summary>
    /// 实例化该组件的 ViewModel
    /// </summary>
    public WidgetViewModel CreateViewModel(bool isDesignMode = false)
    {
        if (ViewModelType == null)
            throw new InvalidOperationException($"组件 {TypeId} 的 ViewModelType 未指定。");

        var instance = Activator.CreateInstance(ViewModelType);
        if (instance is not WidgetViewModel vm)
            throw new InvalidOperationException($"类型 {ViewModelType.FullName} 未继承自 WidgetViewModel。");

        vm.Type = Type;
        vm.IsDesignMode = isDesignMode;
        vm.Width = DefaultWidth;
        vm.Height = DefaultHeight;
        vm.Title = DefaultTitle;

        if (Type == WidgetType.Custom)
        {
            // 为自定义组件保留其全局唯一 TypeId
            vm.Properties["_CustomTypeId"] = TypeId;
        }

        return vm;
    }
}
