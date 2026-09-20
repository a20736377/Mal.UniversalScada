using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mal.UniversalScada.Configurator.Wpf.Models;

/// <summary>
/// 树节点层级类型
/// </summary>
public enum TreeNodeType
{
    /// <summary>一级：通道管理根节点</summary>
    ChannelRoot,

    /// <summary>一级：设备管理根节点</summary>
    DeviceRoot,

    /// <summary>二级：具体通道</summary>
    ChannelItem,

    /// <summary>二级：具体设备</summary>
    DeviceItem,

    /// <summary>三级：具体点位</summary>
    TagItem,

    /// <summary>一级：可视化画面所见即所得设计器根节点</summary>
    UiDesignerRoot,

    /// <summary>一级：用户与权限管理根节点</summary>
    UserManagerRoot
}

/// <summary>
/// 通信与设备工程拓扑树节点
/// </summary>
public partial class TopologyTreeNode : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _icon = "📁";

    [ObservableProperty]
    private TreeNodeType _nodeType;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// 挂载的实际领域数据实体 (ChannelConfig / DeviceNode / TagNode)
    /// </summary>
    public object? DataPayload { get; set; }

    /// <summary>
    /// 父节点引用 (便于向上追溯查找归属)
    /// </summary>
    public TopologyTreeNode? Parent { get; set; }

    public ObservableCollection<TopologyTreeNode> Children { get; } = new();

    public TopologyTreeNode(string id, string title, TreeNodeType type, object? payload = null, string icon = "📁")
    {
        Id = id;
        Title = title;
        NodeType = type;
        DataPayload = payload;
        Icon = icon;
    }

    public void AddChild(TopologyTreeNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }
}
