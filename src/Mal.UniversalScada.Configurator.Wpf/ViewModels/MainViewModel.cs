using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Mal.UniversalScada.Configurator.Wpf.Models;
using Mal.UniversalScada.Configurator.Wpf.Views;
using Mal.UniversalScada.Core.Configuration;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Configurator.Wpf.ViewModels;

/// <summary>
/// 右侧主内容展示模式枚举
/// </summary>
public enum ViewMode
{
    ChannelRoot,    // 通道管理总览
    DeviceRoot,     // 设备管理总览
    ChannelDetail,  // 具体通道编辑
    DeviceDetail,   // 具体设备编辑 (含点位列表)
    TagDetail       // 具体单个点位详细编辑
}

/// <summary>
/// 后台组态配置主界面视图模型。
/// 保持极致轻量：所有核心业务能力（组态持久化、级联清理、导入导出、测试探测）全部委托给 Core.Configuration 引擎处理。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IConfigurationService _configService;
    private readonly IAdminAuthService _authService;
    private readonly ITagImportExportService _importExportService;
    private readonly IChannelTester _channelTester;

    [ObservableProperty]
    private string _currentAdmin = "admin";

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private ViewMode _currentViewMode = ViewMode.DeviceRoot;

    #region 拓扑树 (TreeView)

    public ObservableCollection<TopologyTreeNode> TreeRoots { get; } = new();

    [ObservableProperty]
    private TopologyTreeNode? _selectedTreeNode;

    partial void OnSelectedTreeNodeChanged(TopologyTreeNode? value)
    {
        if (value == null) return;

        switch (value.NodeType)
        {
            case TreeNodeType.ChannelRoot:
                CurrentViewMode = ViewMode.ChannelRoot;
                StatusMessage = "当前选中：通道管理";
                break;

            case TreeNodeType.DeviceRoot:
                CurrentViewMode = ViewMode.DeviceRoot;
                StatusMessage = "当前选中：设备管理";
                break;

            case TreeNodeType.ChannelItem when value.DataPayload is ChannelConfig ch:
                SelectedChannel = ch;
                CurrentViewMode = ViewMode.ChannelDetail;
                StatusMessage = $"正在配置通道: {ch.Name} ({ch.ChannelId})";
                break;

            case TreeNodeType.DeviceItem when value.DataPayload is DeviceNode dev:
                SelectedDevice = dev;
                CurrentViewMode = ViewMode.DeviceDetail;
                RefreshCurrentDeviceTags();
                StatusMessage = $"正在配置设备: {dev.Name} ({dev.DeviceId})";
                break;

            case TreeNodeType.TagItem when value.DataPayload is TagNode tag:
                SelectedTag = tag;
                CurrentViewMode = ViewMode.TagDetail;
                StatusMessage = $"正在配置点位: {tag.Name} ({tag.TagId})";
                break;
        }
    }

    /// <summary>
    /// 从预览列表打开指定通道的详细编辑视图
    /// </summary>
    [RelayCommand]
    public void OpenChannelDetail(ChannelConfig? channel)
    {
        if (channel == null) return;
        SelectedChannel = channel;
        CurrentViewMode = ViewMode.ChannelDetail;
        StatusMessage = $"正在配置通道: {channel.Name} ({channel.ChannelId})";

        var chRoot = TreeRoots.FirstOrDefault(r => r.NodeType == TreeNodeType.ChannelRoot);
        var targetNode = chRoot?.Children.FirstOrDefault(c => c.Id == channel.ChannelId);
        if (targetNode != null)
        {
            SelectedTreeNode = targetNode;
            targetNode.IsSelected = true;
        }
    }

    /// <summary>
    /// 从预览列表打开指定设备的详细编辑视图
    /// </summary>
    [RelayCommand]
    public void OpenDeviceDetail(DeviceNode? device)
    {
        if (device == null) return;
        SelectedDevice = device;
        CurrentViewMode = ViewMode.DeviceDetail;
        RefreshCurrentDeviceTags();
        StatusMessage = $"正在配置设备: {device.Name} ({device.DeviceId})";

        var devRoot = TreeRoots.FirstOrDefault(r => r.NodeType == TreeNodeType.DeviceRoot);
        var targetNode = devRoot?.Children.FirstOrDefault(c => c.Id == device.DeviceId);
        if (targetNode != null)
        {
            SelectedTreeNode = targetNode;
            targetNode.IsSelected = true;
        }
    }

    /// <summary>
    /// 从预览列表打开指定点位的详细编辑视图
    /// </summary>
    [RelayCommand]
    public void OpenTagDetail(TagNode? tag)
    {
        if (tag == null) return;
        SelectedTag = tag;
        CurrentViewMode = ViewMode.TagDetail;
        StatusMessage = $"正在配置点位: {tag.Name} ({tag.TagId})";

        var devRoot = TreeRoots.FirstOrDefault(r => r.NodeType == TreeNodeType.DeviceRoot);
        if (devRoot != null)
        {
            foreach (var devNode in devRoot.Children)
            {
                var tagNode = devNode.Children.FirstOrDefault(t => t.Id == tag.TagId);
                if (tagNode != null)
                {
                    devNode.IsExpanded = true;
                    SelectedTreeNode = tagNode;
                    tagNode.IsSelected = true;
                    break;
                }
            }
        }
    }

    #endregion

    #region 数据实体集合与当前选中项

    public ObservableCollection<ChannelConfig> Channels { get; } = new();

    [ObservableProperty]
    private ChannelConfig? _selectedChannel;

    public ObservableCollection<DeviceNode> Devices { get; } = new();

    [ObservableProperty]
    private DeviceNode? _selectedDevice;

    public ObservableCollection<TagNode> AllTags { get; } = new();

    [ObservableProperty]
    private TagNode? _selectedTag;

    /// <summary>
    /// 当前选中设备下属的点位列表
    /// </summary>
    public ObservableCollection<TagNode> CurrentDeviceTags { get; } = new();

    #endregion

    #region 绑定字典与硬件可选项

    public Array ChannelTypes => Enum.GetValues(typeof(ChannelType));
    public Array ProtocolTypes => Enum.GetValues(typeof(ProtocolType));
    public Array TagDataTypes => Enum.GetValues(typeof(TagDataType));
    public Array TagAccessModes => Enum.GetValues(typeof(TagAccessMode));

    public ObservableCollection<string> AvailableSerialPorts { get; } = new();

    #endregion

    public MainViewModel(
        IConfigurationService configService,
        IAdminAuthService authService,
        ITagImportExportService importExportService,
        IChannelTester channelTester)
    {
        _configService = configService;
        _authService = authService;
        _importExportService = importExportService;
        _channelTester = channelTester;

        CurrentAdmin = _authService.CurrentUser ?? "admin";

        RefreshSerialPorts();
        _ = LoadFromDbAsync();
    }

    [RelayCommand]
    public void RefreshSerialPorts()
    {
        AvailableSerialPorts.Clear();
        foreach (var port in _channelTester.GetAvailableSerialPorts())
        {
            AvailableSerialPorts.Add(port);
        }
    }

    /// <summary>
    /// 调用 Core 组态引擎全量载入数据库中的通道、设备与点位
    /// </summary>
    [RelayCommand]
    public async Task LoadFromDbAsync()
    {
        StatusMessage = "正在通过 Core 引擎加载配置...";
        try
        {
            var configData = await _configService.LoadConfigurationAsync();

            Channels.Clear();
            Devices.Clear();
            AllTags.Clear();

            foreach (var ch in configData.Channels) Channels.Add(ch);
            foreach (var dev in configData.Devices) Devices.Add(dev);
            foreach (var tag in configData.Tags) AllTags.Add(tag);

            SelectedChannel = Channels.FirstOrDefault();
            SelectedDevice = Devices.FirstOrDefault();
            SelectedTag = AllTags.FirstOrDefault();

            RebuildHierarchyTree();
            RefreshCurrentDeviceTags();

            StatusMessage = $"数据库加载完成：{Channels.Count} 个通道，{Devices.Count} 个设备，{AllTags.Count} 个点位。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"从数据库加载失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 重构左侧三级拓扑树：
    /// 一级：设备管理、通道管理
    /// 二级：设备管理下是具体设备；通道管理下是具体通道
    /// 三级：具体设备下是该设备的具体 TagNode
    /// </summary>
    public void RebuildHierarchyTree()
    {
        TreeRoots.Clear();

        // 1. 一级节点：设备管理
        var deviceRootNode = new TopologyTreeNode("ROOT_DEVICE", "🖥️ 设备管理", TreeNodeType.DeviceRoot, null, "🖥️");
        foreach (var dev in Devices)
        {
            var devNode = new TopologyTreeNode(dev.DeviceId, $"📟 {dev.Name} ({dev.ProtocolType})", TreeNodeType.DeviceItem, dev, "📟");

            // 三级：具体设备下的点位列表
            var devTags = AllTags.Where(t => t.DeviceId == dev.DeviceId).ToList();
            foreach (var tag in devTags)
            {
                var tagNode = new TopologyTreeNode(tag.TagId, $"🏷️ {tag.Name} [{tag.Address}]", TreeNodeType.TagItem, tag, "🏷️");
                devNode.AddChild(tagNode);
            }

            deviceRootNode.AddChild(devNode);
        }

        // 2. 一级节点：通道管理
        var channelRootNode = new TopologyTreeNode("ROOT_CHANNEL", "📡 通道管理", TreeNodeType.ChannelRoot, null, "📡");
        foreach (var ch in Channels)
        {
            string icon = ch.ChannelType == ChannelType.SerialPort ? "🔌" : "🌐";
            var chNode = new TopologyTreeNode(ch.ChannelId, $"{icon} {ch.Name} ({ch.ChannelType})", TreeNodeType.ChannelItem, ch, icon);
            channelRootNode.AddChild(chNode);
        }

        TreeRoots.Add(deviceRootNode);
        TreeRoots.Add(channelRootNode);

        // 默认展开一级节点
        deviceRootNode.IsExpanded = true;
        channelRootNode.IsExpanded = true;

        if (SelectedTreeNode == null)
        {
            SelectedTreeNode = deviceRootNode;
        }
    }

    private void RefreshCurrentDeviceTags()
    {
        CurrentDeviceTags.Clear();
        if (SelectedDevice != null)
        {
            var tags = AllTags.Where(t => t.DeviceId == SelectedDevice.DeviceId).ToList();
            foreach (var t in tags) CurrentDeviceTags.Add(t);
        }
    }

    #region 通道管理操作 (简单调用 Core 引擎)

    /// <summary>
    /// 【新建通道】(支持指定 SerialPort 或 TcpClient)
    /// </summary>
    [RelayCommand]
    public void CreateChannel(string? mediumType = null)
    {
        bool isSerial = string.Equals(mediumType, "SerialPort", StringComparison.OrdinalIgnoreCase);
        var type = isSerial ? ChannelType.SerialPort : ChannelType.TcpClient;

        var newCh = _configService.CreateChannel(type, Channels.Count, AvailableSerialPorts.FirstOrDefault());
        Channels.Add(newCh);
        SelectedChannel = newCh;
        CurrentViewMode = ViewMode.ChannelDetail;

        RebuildHierarchyTree();
        StatusMessage = $"已成功创建{(isSerial ? "串口" : "以太网")}通道: {newCh.ChannelId}，请在右侧修改参数并保存";
    }

    /// <summary>
    /// 【删除通道】
    /// </summary>
    [RelayCommand]
    public async Task DeleteChannelAsync(ChannelConfig? targetChannel = null)
    {
        var ch = targetChannel ?? SelectedChannel;
        if (ch == null) return;

        if (MessageBox.Show($"确定要删除通道【{ch.Name} ({ch.ChannelId})】吗？\n删除后该通道下的设备将失去链路绑定！", 
            "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        string id = ch.ChannelId;
        await _configService.DeleteChannelAsync(id);
        Channels.Remove(ch);
        SelectedChannel = Channels.FirstOrDefault();

        RebuildHierarchyTree();
        CurrentViewMode = ViewMode.ChannelRoot;
        StatusMessage = $"已删除通道: {id}";
    }

    #endregion

    #region 设备管理操作 (简单调用 Core 引擎)

    /// <summary>
    /// 【新建设备】(弹出对话框配置，所属通道与协议创建后即锁定)
    /// </summary>
    [RelayCommand]
    public void CreateDevice()
    {
        if (Channels.Count == 0)
        {
            MessageBox.Show("当前尚未创建任何通信通道，请先在【通道管理】中创建至少一个通道后再添加设备！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string defaultChannelId = SelectedChannel?.ChannelId ?? Channels.FirstOrDefault()?.ChannelId ?? string.Empty;
        var dialog = new CreateDeviceDialog(Channels, ProtocolTypes, defaultChannelId, Devices.Count + 1)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.CreatedDevice != null)
        {
            var newDev = dialog.CreatedDevice;
            Devices.Add(newDev);
            SelectedDevice = newDev;
            CurrentViewMode = ViewMode.DeviceDetail;

            RebuildHierarchyTree();
            RefreshCurrentDeviceTags();
            StatusMessage = $"已成功创建设备: {newDev.Name} ({newDev.DeviceId})，通道与协议已锁定";
        }
    }

    /// <summary>
    /// 【删除设备】(级联删除名下点位)
    /// </summary>
    [RelayCommand]
    public async Task DeleteDeviceAsync(DeviceNode? targetDevice = null)
    {
        var dev = targetDevice ?? SelectedDevice;
        if (dev == null) return;

        if (MessageBox.Show($"确定要删除设备【{dev.Name} ({dev.DeviceId})】及其名下的所有点位吗？", 
            "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        string id = dev.DeviceId;
        int deletedTagCount = await _configService.DeleteDeviceAsync(id, AllTags);
        Devices.Remove(dev);

        var tagsToRemove = AllTags.Where(t => t.DeviceId == id).ToList();
        foreach (var t in tagsToRemove)
        {
            AllTags.Remove(t);
        }

        SelectedDevice = Devices.FirstOrDefault();
        RebuildHierarchyTree();
        RefreshCurrentDeviceTags();
        CurrentViewMode = ViewMode.DeviceRoot;
        StatusMessage = $"已删除设备 {id} 及其关联的 {deletedTagCount} 个点位";
    }

    #endregion

    #region 点位管理操作 (简单调用 Core 引擎)

    /// <summary>
    /// 【新建点位】
    /// </summary>
    [RelayCommand]
    public void CreateTag(DeviceNode? targetDevice = null)
    {
        var dev = targetDevice ?? SelectedDevice ?? Devices.FirstOrDefault();
        if (dev == null)
        {
            MessageBox.Show("请先创建或选择一个设备后再添加点位！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int devTagCount = AllTags.Count(t => t.DeviceId == dev.DeviceId);
        var newTag = _configService.CreateTag(dev.DeviceId, devTagCount);

        AllTags.Add(newTag);
        SelectedDevice = dev;
        SelectedTag = newTag;
        CurrentViewMode = ViewMode.TagDetail;

        RebuildHierarchyTree();
        RefreshCurrentDeviceTags();
        StatusMessage = $"已在设备【{dev.Name}】下新建点位: {newTag.TagId}";
    }

    /// <summary>
    /// 【删除点位】
    /// </summary>
    [RelayCommand]
    public async Task DeleteTagAsync(TagNode? targetTag = null)
    {
        var tag = targetTag ?? SelectedTag;
        if (tag == null) return;

        if (MessageBox.Show($"确定要删除点位【{tag.Name} ({tag.TagId})】吗？", 
            "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        string id = tag.TagId;
        await _configService.DeleteTagAsync(id);
        AllTags.Remove(tag);
        CurrentDeviceTags.Remove(tag);

        SelectedTag = CurrentDeviceTags.FirstOrDefault();
        RebuildHierarchyTree();
        if (SelectedTag != null)
        {
            CurrentViewMode = ViewMode.TagDetail;
        }
        else if (SelectedDevice != null)
        {
            CurrentViewMode = ViewMode.DeviceDetail;
        }
        StatusMessage = $"已删除点位: {id}";
    }

    #endregion

    #region 辅助功能：通道测试、CSV 导入导出、保存、改密、注销

    [RelayCommand]
    private async Task TestChannelConnectivityAsync()
    {
        if (SelectedChannel == null)
        {
            MessageBox.Show("请先选择要测试的通信通道！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        StatusMessage = $"正在探测通道 {SelectedChannel.ChannelId} ({SelectedChannel.ChannelType})...";
        var result = await _channelTester.TestChannelAsync(SelectedChannel);

        if (result.IsSuccess)
        {
            StatusMessage = $"测试通过: {result.Message}";
            MessageBox.Show($"✅ 通信握手正常！\n\n通道: {SelectedChannel.Name}\n详情: {result.Message}", 
                "测试结果", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            StatusMessage = $"测试失败: {result.Message}";
            MessageBox.Show($"❌ 通信探测失败！\n\n通道: {SelectedChannel.Name}\n原因: {result.Message}", 
                "测试结果", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private async Task ExportTagsCsvAsync()
    {
        var tagsToExport = SelectedDevice != null 
            ? AllTags.Where(t => t.DeviceId == SelectedDevice.DeviceId).ToList() 
            : AllTags.ToList();

        if (tagsToExport.Count == 0)
        {
            MessageBox.Show("当前没有可导出的点位！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sfd = new SaveFileDialog
        {
            Title = "导出点位组态表 (CSV)",
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = $"Tags_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                await _importExportService.ExportToCsvAsync(tagsToExport, sfd.FileName);
                StatusMessage = $"已成功导出 {tagsToExport.Count} 个点位至: {sfd.FileName}";
                MessageBox.Show($"✅ 成功导出 {tagsToExport.Count} 条点位记录！\n路径: {sfd.FileName}", "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task ImportTagsCsvAsync()
    {
        var ofd = new OpenFileDialog
        {
            Title = "导入点位组态表",
            Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                string? targetDevId = SelectedDevice?.DeviceId;
                var importedTags = await _importExportService.ImportFromCsvAsync(ofd.FileName, targetDevId);

                if (importedTags.Count == 0)
                {
                    MessageBox.Show("未从 CSV 文件中解析到有效点位！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int addCount = 0;
                int updateCount = 0;

                foreach (var tag in importedTags)
                {
                    var existing = AllTags.FirstOrDefault(t => t.TagId == tag.TagId);
                    if (existing != null)
                    {
                        existing.Name = tag.Name;
                        existing.Address = tag.Address;
                        existing.DataType = tag.DataType;
                        existing.AccessMode = tag.AccessMode;
                        existing.ScaleFactor = tag.ScaleFactor;
                        existing.Offset = tag.Offset;
                        existing.Unit = tag.Unit;
                        existing.Deadband = tag.Deadband;
                        existing.ScanIntervalMs = tag.ScanIntervalMs;
                        existing.IsHistorical = tag.IsHistorical;
                        updateCount++;
                    }
                    else
                    {
                        AllTags.Add(tag);
                        addCount++;
                    }
                }

                RebuildHierarchyTree();
                RefreshCurrentDeviceTags();
                StatusMessage = $"CSV 导入完成：新增 {addCount} 条，更新 {updateCount} 条。请点击保存按钮存入数据库。";
                MessageBox.Show($"✅ 导入成功！\n新增: {addCount} 条\n更新: {updateCount} 条", "导入完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// 全量保存组态至 SQLite 数据库（由 Core 引擎统一执行参数清理与批量持久化）
    /// </summary>
    [RelayCommand]
    private async Task SaveAllAsync()
    {
        StatusMessage = "正在通过 Core 引擎保存组态配置到数据库...";
        try
        {
            await _configService.SaveConfigurationAsync(Channels, Devices, AllTags);

            RebuildHierarchyTree();
            StatusMessage = $"组态保存成功！共持久化 {Channels.Count} 个通道，{Devices.Count} 个设备，{AllTags.Count} 个点位。";
            MessageBox.Show("✅ 组态配置已成功存入本地 SQLite 数据库！", "保存完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存配置失败: {ex.Message}";
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenChangePasswordDialog()
    {
        var dlg = new ChangePasswordDialog(_authService, CurrentAdmin)
        {
            Owner = Application.Current.MainWindow
        };
        dlg.ShowDialog();
    }

    [RelayCommand]
    private void Logout()
    {
        if (MessageBox.Show("确定要退出当前管理员登录并关闭系统吗？", "注销确认", 
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _authService.Logout();
            Application.Current.Shutdown();
        }
    }

    #endregion
}
