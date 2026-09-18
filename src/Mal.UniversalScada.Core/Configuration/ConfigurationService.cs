using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Configuration;

/// <summary>
/// 工业 SCADA 核心组态配置管理引擎服务实现类
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IConfigRepository _configRepository;

    public ConfigurationService(IConfigRepository configRepository)
    {
        _configRepository = configRepository;
    }

    public async Task<ScadaConfigurationData> LoadConfigurationAsync()
    {
        var channels = (await _configRepository.GetChannelsAsync()).ToList();
        var devices = (await _configRepository.GetDevicesAsync()).ToList();
        var tags = (await _configRepository.GetAllTagsAsync()).ToList();

        // 若首次运行系统且数据库内无通道数据，自动植入示范初始工程组态
        if (channels.Count == 0)
        {
            var seedData = CreateDefaultSeedData();
            await SaveConfigurationAsync(seedData.Channels, seedData.Devices, seedData.Tags);
            return seedData;
        }

        foreach (var ch in channels)
        {
            CleanUnusedMediaParameters(ch);
        }

        return new ScadaConfigurationData(channels, devices, tags);
    }

    public async Task SaveConfigurationAsync(
        IEnumerable<ChannelConfig> channels,
        IEnumerable<DeviceNode> devices,
        IEnumerable<TagNode> tags)
    {
        foreach (var ch in channels)
        {
            CleanUnusedMediaParameters(ch);
            await _configRepository.SaveChannelAsync(ch);
        }

        foreach (var dev in devices)
        {
            await _configRepository.SaveDeviceAsync(dev);
        }

        await _configRepository.BatchSaveTagsAsync(tags);
    }

    public ChannelConfig CreateChannel(ChannelType channelType, int existingChannelCount, string? preferredSerialPort = null)
    {
        bool isSerial = channelType == ChannelType.SerialPort;

        var ch = new ChannelConfig
        {
            ChannelId = isSerial ? $"CH_COM_{existingChannelCount + 1:D2}" : $"CH_TCP_{existingChannelCount + 1:D2}",
            Name = isSerial ? $"新建串口通道 {existingChannelCount + 1}" : $"新建以太网通道 {existingChannelCount + 1}",
            ChannelType = channelType,
            Host = isSerial ? string.Empty : "192.168.1.100",
            Port = isSerial ? 0 : 502,
            PortName = isSerial ? (preferredSerialPort ?? "COM1") : string.Empty,
            BaudRate = isSerial ? 9600 : 0,
            DataBits = isSerial ? 8 : 0,
            StopBits = isSerial ? "One" : string.Empty,
            Parity = isSerial ? "None" : string.Empty,
            ReadTimeoutMs = 1500,
            WriteTimeoutMs = 1500,
            ReconnectIntervalMs = 5000,
            IsEnabled = true
        };

        return ch;
    }

    public async Task DeleteChannelAsync(string channelId)
    {
        await _configRepository.DeleteChannelAsync(channelId);
    }

    public DeviceNode CreateDevice(
        string deviceId,
        string name,
        string channelId,
        ProtocolType protocolType,
        int stationAddress = 1,
        int pollIntervalMs = 100)
    {
        return new DeviceNode
        {
            DeviceId = deviceId,
            Name = name,
            ChannelId = channelId,
            ProtocolType = protocolType,
            StationAddress = stationAddress,
            DefaultPollIntervalMs = pollIntervalMs,
            TimeoutMs = 2000,
            IsEnabled = true
        };
    }

    public async Task<int> DeleteDeviceAsync(string deviceId, IEnumerable<TagNode> allTags)
    {
        var tagsToDelete = allTags.Where(t => t.DeviceId == deviceId).ToList();
        foreach (var tag in tagsToDelete)
        {
            await _configRepository.DeleteTagAsync(tag.TagId);
        }

        await _configRepository.DeleteDeviceAsync(deviceId);
        return tagsToDelete.Count;
    }

    public TagNode CreateTag(string deviceId, int currentTagCountInDevice)
    {
        return new TagNode
        {
            TagId = $"{deviceId}.Tag_{currentTagCountInDevice + 1:D2}",
            DeviceId = deviceId,
            Name = $"新测点 {currentTagCountInDevice + 1}",
            Address = "40001",
            DataType = TagDataType.Int16,
            AccessMode = TagAccessMode.ReadWrite,
            ScaleFactor = 1.0,
            Offset = 0.0,
            Unit = "",
            Deadband = 0.0,
            ScanIntervalMs = 100,
            IsHistorical = true
        };
    }

    public async Task DeleteTagAsync(string tagId)
    {
        await _configRepository.DeleteTagAsync(tagId);
    }

    public async Task<IReadOnlyList<TagNode>> GetAllTagsAsync()
    {
        var tags = await _configRepository.GetAllTagsAsync();
        return tags.ToList();
    }

    public void CleanUnusedMediaParameters(ChannelConfig channel)
    {
        if (channel.ChannelType == ChannelType.SerialPort)
        {
            channel.Host = string.Empty;
            channel.Port = 0;
        }
        else
        {
            channel.PortName = string.Empty;
            channel.BaudRate = 0;
            channel.DataBits = 0;
            channel.StopBits = string.Empty;
            channel.Parity = string.Empty;
        }
    }

    private ScadaConfigurationData CreateDefaultSeedData()
    {
        var ch1 = new ChannelConfig
        {
            ChannelId = "CH_01",
            Name = "主产线 PLC 以太网通道",
            ChannelType = ChannelType.TcpClient,
            Host = "192.168.1.100",
            Port = 502,
            PortName = string.Empty,
            BaudRate = 0,
            DataBits = 0,
            StopBits = string.Empty,
            Parity = string.Empty,
            ReadTimeoutMs = 1500,
            WriteTimeoutMs = 1500
        };

        var ch2 = new ChannelConfig
        {
            ChannelId = "CH_02",
            Name = "温湿度传感器串口通道",
            ChannelType = ChannelType.SerialPort,
            Host = string.Empty,
            Port = 0,
            PortName = "COM1",
            BaudRate = 9600,
            DataBits = 8,
            StopBits = "One",
            Parity = "None",
            ReadTimeoutMs = 1500,
            WriteTimeoutMs = 1500
        };

        var dev1 = new DeviceNode
        {
            DeviceId = "DEV_01",
            Name = "1号灌装机 PLC",
            ChannelId = ch1.ChannelId,
            ProtocolType = ProtocolType.ModbusTcp,
            StationAddress = 1,
            DefaultPollIntervalMs = 100
        };

        var dev2 = new DeviceNode
        {
            DeviceId = "DEV_02",
            Name = "仓储温湿度监测模块",
            ChannelId = ch2.ChannelId,
            ProtocolType = ProtocolType.Custom,
            CustomProtocolName = "CustomSerial",
            StationAddress = 1,
            DefaultPollIntervalMs = 500
        };

        var tags = new List<TagNode>
        {
            new() { TagId = "DEV_01.Motor_Current", DeviceId = "DEV_01", Name = "主轴电机工作电流", Address = "40002", DataType = TagDataType.Float, AccessMode = TagAccessMode.ReadOnly, ScaleFactor = 1.0, Offset = 0, Unit = "A", ScanIntervalMs = 100, IsHistorical = true },
            new() { TagId = "DEV_01.Motor_Speed", DeviceId = "DEV_01", Name = "主轴电机实时转速1", Address = "40001", DataType = TagDataType.Int16, AccessMode = TagAccessMode.ReadOnly, ScaleFactor = 1.0, Offset = 0, Unit = "rpm", ScanIntervalMs = 100, IsHistorical = true },
            new() { TagId = "DEV_01.System_Start", DeviceId = "DEV_01", Name = "系统启动控制线圈", Address = "00001", DataType = TagDataType.Bool, AccessMode = TagAccessMode.ReadWrite, ScaleFactor = 1.0, Offset = 0, Unit = "", ScanIntervalMs = 100, IsHistorical = true },
            new() { TagId = "DEV_02.Temperature", DeviceId = "DEV_02", Name = "环境当前温度", Address = "TEMP_VAL", DataType = TagDataType.Float, AccessMode = TagAccessMode.ReadOnly, ScaleFactor = 0.1, Offset = 0, Unit = "℃", ScanIntervalMs = 500, IsHistorical = true },
            new() { TagId = "DEV_02.Humidity", DeviceId = "DEV_02", Name = "环境当前湿度", Address = "HUMI_VAL", DataType = TagDataType.Float, AccessMode = TagAccessMode.ReadOnly, ScaleFactor = 0.1, Offset = 0, Unit = "%RH", ScanIntervalMs = 500, IsHistorical = true }
        };

        return new ScadaConfigurationData(
            new List<ChannelConfig> { ch1, ch2 },
            new List<DeviceNode> { dev1, dev2 },
            tags);
    }

    #region 界面组态视图 (UiViews)

    public async Task<IReadOnlyList<UiViewConfig>> GetUiViewsAsync()
    {
        return await _configRepository.GetUiViewsAsync();
    }

    public async Task<UiViewConfig?> GetUiViewByIdAsync(string viewId)
    {
        return await _configRepository.GetUiViewByIdAsync(viewId);
    }

    public async Task SaveUiViewAsync(UiViewConfig view)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.UpdatedTime = DateTime.Now;
        await _configRepository.SaveUiViewAsync(view);
    }

    public async Task DeleteUiViewAsync(string viewId)
    {
        await _configRepository.DeleteUiViewAsync(viewId);
    }

    public UiViewConfig CreateDefaultUiView(string? name = null, string? deviceId = null)
    {
        return new UiViewConfig
        {
            ViewId = "View_" + Guid.NewGuid().ToString("N")[..8],
            Name = name ?? "新建工艺看板",
            BoundDeviceId = deviceId,
            LayoutMode = "Canvas",
            CanvasWidth = 1920,
            CanvasHeight = 1080,
            IsDefault = false,
            Widgets = new List<WidgetConfig>(),
            UpdatedTime = DateTime.Now
        };
    }

    #endregion
}
