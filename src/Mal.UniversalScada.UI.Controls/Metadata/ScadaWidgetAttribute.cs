using System;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.UI.Controls.Metadata;

/// <summary>
/// SCADA 工业组件元数据标记特性。
/// 用于标注 WidgetViewModel 派生类，使其能够被 WidgetRegistry 自动反射发现并注册到工具箱、画布及属性编辑器。
/// 支持来自当前类库或外部扩展类库的任何组件。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ScadaWidgetAttribute : Attribute
{
    /// <summary>
    /// 组件全局唯一标识（若为标准组件则为枚举名，自定义组件可为字符串如 "Vendor.RobotArm"）
    /// </summary>
    public new string TypeId { get; set; }

    /// <summary>
    /// 组件枚举类型（内置标准组件为具体枚举，第三方扩展组件默认为 WidgetType.Custom）
    /// </summary>
    public WidgetType Type { get; set; }

    /// <summary>
    /// 工具箱与设计器展示名称（如 "180° 拱形仪表"）
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// 工具箱图标（如 "🧭", "📊", "💡"）
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// 组件分组类别（如 "仪表类", "工业图元", "控制交互", "第三方扩展"）
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// 功能详细描述与设计指引
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// 工具箱中同类别内的排序权重（越小越靠前）
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 默认宽度 (像素)
    /// </summary>
    public double DefaultWidth { get; set; }

    /// <summary>
    /// 默认高度 (像素)
    /// </summary>
    public double DefaultHeight { get; set; }

    /// <summary>
    /// 拖放到画布时的默认标题 (如 "反应釜温度", "主轴转速")
    /// </summary>
    public string DefaultTitle { get; set; }

    /// <summary>
    /// 对应的 XAML 视图 UserControl 类型（可选，若未指定则通过规则匹配）
    /// </summary>
    public Type? ViewType { get; set; }

    /// <summary>
    /// 对应的专属属性编辑器 UserControl 类型（可选）
    /// </summary>
    public Type? PropertyEditorType { get; set; }

    /// <summary>
    /// 标准内置组件构造函数
    /// </summary>
    public ScadaWidgetAttribute(
        WidgetType type,
        string displayName,
        string icon = "📦",
        string category = "通用组件",
        string description = "",
        int order = 100,
        double defaultWidth = 180,
        double defaultHeight = 140,
        string? defaultTitle = null,
        Type? viewType = null,
        Type? propertyEditorType = null)
    {
        Type = type;
        TypeId = type.ToString();
        DisplayName = displayName;
        Icon = icon;
        Category = category;
        Description = description;
        Order = order;
        DefaultWidth = defaultWidth;
        DefaultHeight = defaultHeight;
        DefaultTitle = defaultTitle ?? displayName;
        ViewType = viewType;
        PropertyEditorType = propertyEditorType;
    }

    /// <summary>
    /// 第三方/自定义扩展类库组件构造函数 (简易构造)
    /// </summary>
    public ScadaWidgetAttribute(string typeId)
    {
        TypeId = typeId;
        Type = WidgetType.Custom;
        DisplayName = typeId;
        Icon = "🧩";
        Category = "第三方扩展";
        Description = string.Empty;
        Order = 500;
        DefaultWidth = 180;
        DefaultHeight = 140;
        DefaultTitle = typeId;
    }

    /// <summary>
    /// 第三方/自定义扩展类库组件构造函数
    /// </summary>
    public ScadaWidgetAttribute(
        string typeId,
        string displayName,
        string icon = "🧩",
        string category = "第三方扩展",
        string description = "",
        int order = 500,
        double defaultWidth = 180,
        double defaultHeight = 140,
        string? defaultTitle = null,
        Type? viewType = null,
        Type? propertyEditorType = null)
    {
        TypeId = typeId;
        Type = WidgetType.Custom;
        DisplayName = displayName;
        Icon = icon;
        Category = category;
        Description = description;
        Order = order;
        DefaultWidth = defaultWidth;
        DefaultHeight = defaultHeight;
        DefaultTitle = defaultTitle ?? displayName;
        ViewType = viewType;
        PropertyEditorType = propertyEditorType;
    }
}
