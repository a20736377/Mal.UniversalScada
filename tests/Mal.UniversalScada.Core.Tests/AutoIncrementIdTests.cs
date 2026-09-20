using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Storage.Sqlite;
using Xunit;

namespace Mal.UniversalScada.Core.Tests;

public class AutoIncrementIdTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connStr;

    public AutoIncrementIdTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"scada_test_{Guid.NewGuid():N}.db");
        _connStr = $"Data Source={_testDbPath}";
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch { }
    }

    [Fact]
    public async Task Channel_Device_Tag_UiView_AutoIncrementIds_WorkCorrectly()
    {
        var repo = new SqliteConfigRepository(_connStr);

        // 1. Channel insert
        var ch1 = new ChannelConfig { Name = "产线以太网1", ChannelType = ChannelType.TcpClient, Host = "127.0.0.1", Port = 502 };
        var ch2 = new ChannelConfig { Name = "产线以太网2", ChannelType = ChannelType.TcpClient, Host = "127.0.0.1", Port = 503 };

        await repo.SaveChannelAsync(ch1);
        await repo.SaveChannelAsync(ch2);

        Assert.True(ch1.Id > 0, "Channel 1 Id should be assigned");
        Assert.True(ch2.Id > ch1.Id, "Channel 2 Id should increment");

        var channels = await repo.GetChannelsAsync();
        Assert.Equal(2, channels.Count);
        Assert.Equal(ch1.Id, channels[0].Id);
        Assert.Equal(ch2.Id, channels[1].Id);

        // 2. Device insert
        var dev1 = new DeviceNode { Name = "灌装PLC1", ChannelId = ch1.ChannelId, ProtocolType = ProtocolType.ModbusTcp };
        var dev2 = new DeviceNode { Name = "灌装PLC2", ChannelId = ch1.ChannelId, ProtocolType = ProtocolType.ModbusTcp };

        await repo.SaveDeviceAsync(dev1);
        await repo.SaveDeviceAsync(dev2);

        Assert.True(dev1.Id > 0, "Device 1 Id should be assigned");
        Assert.True(dev2.Id > dev1.Id, "Device 2 Id should increment");

        var devices = await repo.GetDevicesAsync();
        Assert.Equal(2, devices.Count);
        Assert.Equal(dev1.Id, devices[0].Id);
        Assert.Equal(dev2.Id, devices[1].Id);

        // 3. Tag insert & batch save
        var tag1 = new TagNode { DeviceId = dev1.DeviceId, Name = "温度1", Address = "40001", DataType = TagDataType.Float };
        var tag2 = new TagNode { DeviceId = dev1.DeviceId, Name = "压力1", Address = "40003", DataType = TagDataType.Float };

        await repo.SaveTagAsync(tag1);
        Assert.True(tag1.Id > 0, "Tag 1 Id should be assigned");

        var tag3 = new TagNode { DeviceId = dev1.DeviceId, Name = "转速1", Address = "40005", DataType = TagDataType.Int16 };
        await repo.BatchSaveTagsAsync(new[] { tag2, tag3 });

        Assert.True(tag2.Id > tag1.Id, "Tag 2 Id should increment");
        Assert.True(tag3.Id > tag2.Id, "Tag 3 Id should increment");

        var tags = await repo.GetAllTagsAsync();
        Assert.Equal(3, tags.Count);
        Assert.Equal(tag1.Id, tags[0].Id);
        Assert.Equal(tag2.Id, tags[1].Id);
        Assert.Equal(tag3.Id, tags[2].Id);

        // 验证删除点位 (使用 long Id) 与新建点位不复用已删除 Id
        await repo.DeleteTagAsync(tag2.Id);
        var tagsAfterDelete = await repo.GetAllTagsAsync();
        Assert.Equal(2, tagsAfterDelete.Count);
        Assert.DoesNotContain(tagsAfterDelete, t => t.Id == tag2.Id);

        var tag4 = new TagNode { DeviceId = dev1.DeviceId, Name = "流量1", Address = "40007", DataType = TagDataType.Float };
        await repo.SaveTagAsync(tag4);
        Assert.True(tag4.Id > tag3.Id, "Tag 4 Id should be strictly monotonic after deletion");

        // 4. UiView insert
        var v1 = new UiViewConfig { Name = "主监控看板1" };
        var v2 = new UiViewConfig { Name = "主监控看板2" };

        await repo.SaveUiViewAsync(v1);
        await repo.SaveUiViewAsync(v2);

        Assert.True(v1.Id > 0, "View 1 Id should be assigned");
        Assert.True(v2.Id > v1.Id, "View 2 Id should increment");

        var views = await repo.GetUiViewsAsync();
        Assert.Equal(2, views.Count);
        Assert.Contains(views, v => v.Id == v1.Id && v.Name == "主监控看板1");
        Assert.Contains(views, v => v.Id == v2.Id && v.Name == "主监控看板2");
    }

    [Fact]
    public async Task User_AutoIncrementId_WorksCorrectly()
    {
        var userRepo = new SqliteUserRepository(_connStr);

        var users = await userRepo.GetAllUsersAsync();
        // Seed users (admin, engineer, operator)
        Assert.True(users.Count >= 3);
        Assert.True(users[0].Id > 0);
        Assert.True(users[1].Id > users[0].Id);
        Assert.True(users[2].Id > users[1].Id);

        // Add custom user
        var customUser = new UserInfo
        {
            Username = "shift_leader",
            DisplayName = "值班领班",
            Role = UserRole.Operator,
            IsEnabled = true
        };

        await userRepo.SaveUserAsync(customUser, "leader123");
        Assert.True(customUser.Id > users.Last().Id, "New user should get auto-incremented Id");

        var reloadedUsers = await userRepo.GetAllUsersAsync();
        var found = reloadedUsers.FirstOrDefault(u => u.Username == "shift_leader");
        Assert.NotNull(found);
        Assert.Equal(customUser.Id, found.Id);
    }

    [Fact]
    public async Task LegacyDatabase_WithoutIdColumns_AutoMigratesSmoothly()
    {
        // 模拟一个旧版没有自增 Id 列的数据库
        await using (var conn = new SqliteConnection(_connStr))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(@"
                CREATE TABLE Channels (
                    ChannelId TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    ChannelType INTEGER NOT NULL,
                    PortName TEXT,
                    BaudRate INTEGER NOT NULL,
                    DataBits INTEGER NOT NULL,
                    StopBits TEXT,
                    Parity TEXT,
                    Host TEXT,
                    Port INTEGER NOT NULL,
                    ReadTimeoutMs INTEGER NOT NULL,
                    WriteTimeoutMs INTEGER NOT NULL,
                    ReconnectIntervalMs INTEGER NOT NULL,
                    IsEnabled INTEGER NOT NULL
                );
                INSERT INTO Channels (ChannelId, Name, ChannelType, PortName, BaudRate, DataBits, StopBits, Parity, Host, Port, ReadTimeoutMs, WriteTimeoutMs, ReconnectIntervalMs, IsEnabled)
                VALUES ('LEGACY_CH_01', '老旧通道', 2, '', 0, 0, '', '', '192.168.0.10', 502, 1000, 1000, 5000, 1);
            ");
        }

        // 使用 SqliteConfigRepository 初始化，触发自动平滑迁移
        var repo = new SqliteConfigRepository(_connStr);
        var channels = await repo.GetChannelsAsync();

        Assert.Single(channels);
        Assert.Equal("LEGACY_CH_01", channels[0].ChannelId);
        Assert.True(channels[0].Id > 0, "Legacy channel should be assigned an auto-increment Id upon migration");

        // 验证后续新建通道自增生效
        var newCh = new ChannelConfig { Name = "迁移后新建通道", ChannelType = ChannelType.TcpClient, Host = "10.0.0.1", Port = 502 };
        await repo.SaveChannelAsync(newCh);
        Assert.True(newCh.Id > channels[0].Id, "New channel should have a higher auto-increment Id");
    }

    [Fact]
    public async Task DeletionAndRecreation_DoesNotCollide_AndUpdatesUsePrimaryKey()
    {
        var repo = new SqliteConfigRepository(_connStr);

        var ch = new ChannelConfig { Name = "主通道", ChannelType = ChannelType.TcpClient };
        await repo.SaveChannelAsync(ch);

        // 插入3个设备
        var dev1 = new DeviceNode { Name = "设备1", ChannelId = ch.ChannelId, ProtocolType = ProtocolType.ModbusTcp };
        var dev2 = new DeviceNode { Name = "设备2", ChannelId = ch.ChannelId, ProtocolType = ProtocolType.ModbusTcp };
        var dev3 = new DeviceNode { Name = "设备3", ChannelId = ch.ChannelId, ProtocolType = ProtocolType.ModbusTcp };

        await repo.SaveDeviceAsync(dev1);
        await repo.SaveDeviceAsync(dev2);
        await repo.SaveDeviceAsync(dev3);

        Assert.Equal(1, dev1.Id);
        Assert.Equal(2, dev2.Id);
        Assert.Equal(3, dev3.Id);

        // 删除设备 2
        await repo.DeleteDeviceAsync(dev2.DeviceId);
        var remainingDevices = await repo.GetDevicesAsync();
        Assert.Equal(2, remainingDevices.Count);
        Assert.DoesNotContain(remainingDevices, d => d.Id == 2);

        // 模拟创建新设备（即使传入了可能冲突的旧 DEV_02 或 DEV_03，系统自增也能避免冲突）
        var dev4 = new DeviceNode { DeviceId = "DEV_03", Name = "设备4", ChannelId = ch.ChannelId, ProtocolType = ProtocolType.ModbusTcp };
        await repo.SaveDeviceAsync(dev4);

        // 验证：
        // 1. dev4 不会覆盖原有的 dev3
        // 2. dev4 的 Id 为 4（SQLite 绝不复用已删除的 2 或 3）
        // 3. dev4 的 DeviceId 自动格式化为唯一标识 DEV_04
        Assert.Equal(4, dev4.Id);
        Assert.Equal("DEV_04", dev4.DeviceId);

        var allDevs = await repo.GetDevicesAsync();
        Assert.Equal(3, allDevs.Count);
        Assert.Contains(allDevs, d => d.Id == 1 && d.Name == "设备1");
        Assert.Contains(allDevs, d => d.Id == 3 && d.Name == "设备3");
        Assert.Contains(allDevs, d => d.Id == 4 && d.Name == "设备4");

        // 验证更新：严格以 Id 为条件更新设备 1 的名称和属性
        dev1.Name = "设备1_已修改";
        await repo.SaveDeviceAsync(dev1);

        var reloadedDev1 = await repo.GetDeviceByIdAsync(dev1.DeviceId);
        Assert.NotNull(reloadedDev1);
        Assert.Equal("设备1_已修改", reloadedDev1.Name);

        // 验证设备 3 和设备 4 保持不受任何影响
        var reloadedDev3 = await repo.GetDeviceByIdAsync(dev3.DeviceId);
        Assert.NotNull(reloadedDev3);
        Assert.Equal("设备3", reloadedDev3.Name);
    }
}
