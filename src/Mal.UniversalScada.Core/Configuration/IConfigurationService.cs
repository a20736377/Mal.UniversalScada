using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Configuration;

/// <summary>
/// 组态配置全量载入数据包
/// </summary>
public record ScadaConfigurationData(
    List<ChannelConfig> Channels,
    List<DeviceNode> Devices,
    List<TagNode> Tags);

/// <summary>
/// 工业 SCADA 核心组态配置管理引擎服务接口。
/// 封装全部通道、设备、点位的业务逻辑、参数规范性校验、级联清理及仓储持久化操作。
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// 加载当前系统的全量组态（通道、设备、点位）。若数据库为空，则自动执行初始默认示范组态植入。
    /// </summary>
    Task<ScadaConfigurationData> LoadConfigurationAsync();

    /// <summary>
    /// 批量保存全量组态至数据库，自动规范清理与传输介质不匹配的冗余参数
    /// </summary>
    Task SaveConfigurationAsync(
        IEnumerable<ChannelConfig> channels,
        IEnumerable<DeviceNode> devices,
        IEnumerable<TagNode> tags);

    /// <summary>
    /// 创建并初始化一个新的通信通道（自动生成符合介质规范的初始参数）
    /// </summary>
    ChannelConfig CreateChannel(ChannelType channelType, int existingChannelCount, string? preferredSerialPort = null);

    /// <summary>
    /// 删除指定通道
    /// </summary>
    Task DeleteChannelAsync(string channelId);

    /// <summary>
    /// 创建并初始化一个新的下位机设备（所属通道与通信协议在此确定）
    /// </summary>
    DeviceNode CreateDevice(
        string deviceId,
        string name,
        string channelId,
        ProtocolType protocolType,
        int stationAddress = 1,
        int pollIntervalMs = 100);

    /// <summary>
    /// 删除指定设备，并级联删除归属于该设备的所有点位
    /// </summary>
    /// <returns>级联删除的点位数量</returns>
    Task<int> DeleteDeviceAsync(string deviceId, IEnumerable<TagNode> allTags);

    /// <summary>
    /// 在指定设备下创建并初始化一个新的点位
    /// </summary>
    TagNode CreateTag(string deviceId, int currentTagCountInDevice);

    /// <summary>
    /// 删除指定点位
    /// </summary>
    Task DeleteTagAsync(string tagId);

    /// <summary>
    /// 清理不属于当前传输介质的冗余参数，保存成空
    /// </summary>
    void CleanUnusedMediaParameters(ChannelConfig channel);
}
