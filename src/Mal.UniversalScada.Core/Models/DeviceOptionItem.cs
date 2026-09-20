using System;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Core.Models;

/// <summary>
/// 设计器设备下拉选项模型（用于设备状态卡片关联具体通信设备）
/// </summary>
public class DeviceOptionItem
{
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public ProtocolType ProtocolType { get; set; } = ProtocolType.ModbusTcp;
    public int StationAddress { get; set; } = 1;
    public int DefaultPollIntervalMs { get; set; } = 100;
    public int TagCount { get; set; } = 0;
    public string DisplayText { get; set; } = string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(DisplayText) ? Name : DisplayText;

    public static DeviceOptionItem CreateUnbound() => new()
    {
        DeviceId = string.Empty,
        Name = "-- 未绑定设备 --",
        DisplayText = "-- 未关联任何具体设备 (静态示范) --"
    };

    public static DeviceOptionItem FromDevice(DeviceNode device, int tagCount = 0) => new()
    {
        DeviceId = device.DeviceId,
        Name = device.Name,
        ChannelId = device.ChannelId,
        ProtocolType = device.ProtocolType,
        StationAddress = device.StationAddress,
        DefaultPollIntervalMs = device.DefaultPollIntervalMs,
        TagCount = tagCount,
        DisplayText = $"{device.Name} [{device.DeviceId}] ({device.ProtocolType}, 站号:{device.StationAddress})"
    };

    public static DeviceOptionItem FromDeviceNode(DeviceNode device, int tagCount = 0) => FromDevice(device, tagCount);
}
