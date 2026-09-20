using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 系统组态配置仓储接口。
/// 负责通道参数、设备拓扑、点位字典及报警规则的持久化管理与配置热加载。
/// 底层由关系型数据库（如 SQLite / PostgreSQL）提供事务与主外键保障。
/// </summary>
public interface IConfigRepository
{
    #region 通道配置 (Channels)

    /// <summary>
    /// 获取系统中定义的所有通信通道列表
    /// </summary>
    Task<IReadOnlyList<ChannelConfig>> GetChannelsAsync();

    /// <summary>
    /// 根据通道 ID 获取通道配置
    /// </summary>
    Task<ChannelConfig?> GetChannelByIdAsync(string channelId);

    /// <summary>
    /// 保存或更新通道配置
    /// </summary>
    Task SaveChannelAsync(ChannelConfig channel);

    /// <summary>
    /// 删除指定通道配置
    /// </summary>
    Task DeleteChannelAsync(string channelId);

    #endregion

    #region 设备节点 (Devices)

    /// <summary>
    /// 获取系统中定义的所有设备节点列表
    /// </summary>
    Task<IReadOnlyList<DeviceNode>> GetDevicesAsync();

    /// <summary>
    /// 根据设备 ID 获取设备详情
    /// </summary>
    Task<DeviceNode?> GetDeviceByIdAsync(string deviceId);

    /// <summary>
    /// 获取挂载在指定通道下的所有设备
    /// </summary>
    Task<IReadOnlyList<DeviceNode>> GetDevicesByChannelAsync(string channelId);

    /// <summary>
    /// 保存或更新设备配置
    /// </summary>
    Task SaveDeviceAsync(DeviceNode device);

    /// <summary>
    /// 删除指定设备
    /// </summary>
    Task DeleteDeviceAsync(string deviceId);

    #endregion

    #region 点位组态 (Tags)

    /// <summary>
    /// 获取指定设备下的所有点位定义
    /// </summary>
    /// <param name="deviceId">设备 ID</param>
    Task<IReadOnlyList<TagNode>> GetTagsByDeviceAsync(string deviceId);

    /// <summary>
    /// 获取系统全量点位元数据定义
    /// </summary>
    Task<IReadOnlyList<TagNode>> GetAllTagsAsync();

    /// <summary>
    /// 保存或更新点位定义
    /// </summary>
    /// <param name="tag">点位对象</param>
    Task SaveTagAsync(TagNode tag);

    /// <summary>
    /// 批量保存或更新点位定义（如从 Excel / CSV 批量导入组态表）
    /// </summary>
    /// <param name="tags">点位集合</param>
    Task BatchSaveTagsAsync(IEnumerable<TagNode> tags);

    /// <summary>
    /// 删除指定点位
    /// </summary>
    /// <param name="id">点位自增数字主键 ID</param>
    Task DeleteTagAsync(long id);

    #endregion

    #region 界面组态视图 (UiViews)

    /// <summary>
    /// 获取系统中配置的所有监控画面列表
    /// </summary>
    Task<IReadOnlyList<UiViewConfig>> GetUiViewsAsync();

    /// <summary>
    /// 根据画面 ID 获取画面配置
    /// </summary>
    Task<UiViewConfig?> GetUiViewByIdAsync(string viewId);

    /// <summary>
    /// 保存或更新画面组态配置
    /// </summary>
    Task SaveUiViewAsync(UiViewConfig view);

    /// <summary>
    /// 删除指定画面配置
    /// </summary>
    Task DeleteUiViewAsync(string viewId);

    #endregion
}
