using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Configuration;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Core.Services;
using Mal.UniversalScada.Storage.Sqlite;

namespace Mal.UniversalScada.Core.Tests;

public class CoreEnginesTests
{
    #region 1. RealtimeDataBus Tests (实时数据总线)

    [Fact]
    public void RealtimeDataBus_PublishAndSnapshot_ShouldWorkCorrectly()
    {
        var bus = new RealtimeDataBus();

        var snap1 = new TagValueSnapshot
        {
            TagId = "Oven.Temp_Zone1",
            Value = 185.5,
            RawValue = 1855,
            Quality = QualityCode.Good,
            Timestamp = DateTime.Now
        };

        bus.PublishSnapshot(snap1);

        var retrieved = bus.GetSnapshot("Oven.Temp_Zone1");
        Assert.NotNull(retrieved);
        Assert.Equal(185.5, retrieved!.Value);
        Assert.Equal(QualityCode.Good, retrieved.Quality);
        Assert.Equal(1, bus.RegisteredTagCount);

        var all = bus.GetAllSnapshots();
        Assert.True(all.ContainsKey("Oven.Temp_Zone1"));
    }

    [Fact]
    public void RealtimeDataBus_Deadband_Filtering_ShouldPreventSmallJitter()
    {
        var bus = new RealtimeDataBus();

        // 注册带死区 1.0 的点位
        bus.RegisterTag(new TagNode
        {
            TagId = "Tank1.Pressure",
            Deadband = 1.0
        });

        int notifyCount = 0;
        using var sub = bus.Subscribe("Tank1.Pressure", s => notifyCount++);

        // 初始值 (首次写入触发通知)
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "Tank1.Pressure", Value = 10.0, Quality = QualityCode.Good });
        Assert.Equal(1, notifyCount);

        // 变动 0.4 (< 1.0 Deadband) -> 不触发通知
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "Tank1.Pressure", Value = 10.4, Quality = QualityCode.Good });
        Assert.Equal(1, notifyCount);

        // 变动 0.8 (< 1.0 Deadband 相对上一次有效通知 10.0) -> 不触发通知
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "Tank1.Pressure", Value = 10.8, Quality = QualityCode.Good });
        Assert.Equal(1, notifyCount);

        // 变动至 11.5 (与上次通知差 1.5 >= 1.0) -> 触发通知
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "Tank1.Pressure", Value = 11.5, Quality = QualityCode.Good });
        Assert.Equal(2, notifyCount);

        // 数值虽然相同，但品质突变为 Bad -> 必须触发通知
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "Tank1.Pressure", Value = 11.5, Quality = QualityCode.Bad });
        Assert.Equal(3, notifyCount);
    }

    [Fact]
    public void RealtimeDataBus_SubscribeAll_And_Dispose_Token_ShouldWork()
    {
        var bus = new RealtimeDataBus();
        var received = new List<TagValueSnapshot>();

        var token = bus.SubscribeAll(s => received.Add(s));

        bus.PublishSnapshot(new TagValueSnapshot { TagId = "TagA", Value = 1, Quality = QualityCode.Good });
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "TagB", Value = 2, Quality = QualityCode.Good });
        Assert.Equal(2, received.Count);

        // 取消订阅
        token.Dispose();
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "TagC", Value = 3, Quality = QualityCode.Good });

        // 不应再收到新推送
        Assert.Equal(2, received.Count);
    }

    #endregion

    #region 2. PriorityScheduler Tests (双优先级调度引擎)

    [Fact]
    public async Task PriorityScheduler_StartAndWritePriority_Preemption_Test()
    {
        var bus = new RealtimeDataBus();

        // 模拟组态：1个TCP通道，1个PLC设备，1个启停控制点位，1个转速读取点位
        var channel = new ChannelConfig
        {
            ChannelId = "CH_TEST_01",
            ChannelType = ChannelType.TcpClient,
            IsEnabled = true
        };
        var device = new DeviceNode
        {
            DeviceId = "DEV_TEST_PLC",
            ChannelId = "CH_TEST_01",
            ProtocolType = ProtocolType.ModbusTcp,
            DefaultPollIntervalMs = 50,
            IsEnabled = true
        };
        var ctrlTag = new TagNode
        {
            TagId = "Motor.Run_Cmd",
            DeviceId = "DEV_TEST_PLC",
            AccessMode = TagAccessMode.ReadWrite,
            Address = "00001"
        };
        var speedTag = new TagNode
        {
            TagId = "Motor.Current_Speed",
            DeviceId = "DEV_TEST_PLC",
            AccessMode = TagAccessMode.ReadOnly,
            Address = "40001"
        };

        var configService = new MockConfigurationService(channel, device, new[] { ctrlTag, speedTag });
        var channelFactory = new MockChannelFactory();
        var driverFactory = new MockDriverFactory();

        var scheduler = new PriorityScheduler(configService, channelFactory, driverFactory, bus);

        Assert.False(scheduler.IsRunning);
        await scheduler.StartAsync();
        Assert.True(scheduler.IsRunning);

        // 等待轮询周期运行，检查测点是否已被读取推入总线
        await Task.Delay(120);
        var speedSnap = bus.GetSnapshot("Motor.Current_Speed");
        Assert.NotNull(speedSnap);
        Assert.Equal(QualityCode.Good, speedSnap!.Quality);

        // 提交高优写控制指令 (启停置 1)
        var writeRes = await scheduler.EnqueueWriteAsync("Motor.Run_Cmd", 1);
        Assert.True(writeRes.IsSuccess);
        Assert.Equal("Motor.Run_Cmd", writeRes.TagId);

        // 验证总线中的写结果快照已同步
        var ctrlSnap = bus.GetSnapshot("Motor.Run_Cmd");
        Assert.NotNull(ctrlSnap);
        Assert.Equal(1, ctrlSnap!.Value);

        await scheduler.StopAsync();
        Assert.False(scheduler.IsRunning);
    }

    #endregion

    #region 3. AlarmEngine Tests (多级报警引擎与确认状态机)

    [Fact]
    public void AlarmEngine_HighAlarm_And_Hysteresis_Lifecycle_Test()
    {
        var bus = new RealtimeDataBus();
        using var engine = new AlarmEngine(bus);

        // 注册高限报警规则：阈值 100，回差 2.0 (即升至 >=100 报警，必须降到 <98 解除)
        var rule = new AlarmRule
        {
            RuleId = "RULE_HI_TEMP",
            TagId = "Furnace.Temp",
            RuleType = AlarmRuleType.High,
            Threshold = 100.0,
            Deadband = 2.0,
            Severity = AlarmSeverity.Critical
        };
        engine.RegisterRule(rule);

        AlarmEvent? raisedAlarm = null;
        AlarmEvent? clearedAlarm = null;
        engine.AlarmRaised += (s, e) => raisedAlarm = e;
        engine.AlarmCleared += (s, e) => clearedAlarm = e;

        // 1. 正常工况 95.0
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "Furnace.Temp", Value = 95.0, Quality = QualityCode.Good });
        Assert.Null(raisedAlarm);
        Assert.Empty(engine.GetActiveAlarms());

        // 2. 升温至 101.5 -> 越限触发报警
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "Furnace.Temp", Value = 101.5, Quality = QualityCode.Good });
        Assert.NotNull(raisedAlarm);
        Assert.Equal("Furnace.Temp", raisedAlarm!.TagId);
        Assert.Equal(AlarmSeverity.Critical, raisedAlarm.Severity);
        Assert.False(raisedAlarm.IsAcknowledged);
        Assert.Single(engine.GetActiveAlarms());

        // 3. 温度回落到 99.0 (虽 < 100，但未低于 100 - 2 = 98 迟滞) -> 报警依然维持
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "Furnace.Temp", Value = 99.0, Quality = QualityCode.Good });
        Assert.Null(clearedAlarm);
        Assert.Single(engine.GetActiveAlarms());

        // 4. 温度降至 97.5 (< 98.0) -> 触发消除
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "Furnace.Temp", Value = 97.5, Quality = QualityCode.Good });
        Assert.NotNull(clearedAlarm);
        Assert.NotNull(clearedAlarm!.ClearedTime);
    }

    [Fact]
    public async Task AlarmEngine_Acknowledge_And_AcknowledgeAll_Test()
    {
        var bus = new RealtimeDataBus();
        using var engine = new AlarmEngine(bus);

        var ruleA = new AlarmRule { RuleId = "R1", TagId = "Tag1", RuleType = AlarmRuleType.High, Threshold = 50.0 };
        var ruleB = new AlarmRule { RuleId = "R2", TagId = "Tag2", RuleType = AlarmRuleType.High, Threshold = 50.0 };
        engine.RegisterRules(new[] { ruleA, ruleB });

        // 触发两条报警
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "Tag1", Value = 60.0, Quality = QualityCode.Good });
        bus.PublishSnapshot(new TagValueSnapshot { TagId = "Tag2", Value = 70.0, Quality = QualityCode.Good });

        var active = engine.GetActiveAlarms();
        Assert.Equal(2, active.Count);
        Assert.All(active, a => Assert.False(a.IsAcknowledged));

        // 单条确认
        bool ackSuccess = await engine.AcknowledgeAlarmAsync(active[0].EventId, "Operator_01");
        Assert.True(ackSuccess);
        Assert.True(active[0].IsAcknowledged);
        Assert.Equal("Operator_01", active[0].AcknowledgedBy);

        // 全部一键确认
        await engine.AcknowledgeAllAlarmsAsync("Supervisor_02");
        var activeAfterAll = engine.GetActiveAlarms();
        Assert.All(activeAfterAll, a => Assert.True(a.IsAcknowledged));
    }

    #endregion

    #region 4. SqliteHistoryRepository & HistoryArchiveWorker Tests (时序数据仓储与归档)

    [Fact]
    public async Task SqliteHistoryRepository_BatchInsert_And_DownsampledQuery_Test()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"scada_hist_{Guid.NewGuid():N}.db");
        var repo = new SqliteHistoryRepository($"Data Source={tempDb}");

        try
        {
            var startTime = new DateTime(2026, 9, 20, 8, 0, 0);
            var records = new List<TagHistoryRecord>();

            // 生成 1000 条正弦波动历史时序数据
            for (int i = 0; i < 1000; i++)
            {
                records.Add(new TagHistoryRecord
                {
                    TagId = "Mixer.Power",
                    Timestamp = startTime.AddSeconds(i),
                    Value = 50.0 + Math.Sin(i * 0.1) * 20.0,
                    Quality = QualityCode.Good
                });
            }

            // 批量高速入库
            int inserted = await repo.InsertBatchAsync(records);
            Assert.Equal(1000, inserted);

            // 原始查询验证
            var raw = await repo.QueryRawHistoryAsync("Mixer.Power", startTime, startTime.AddSeconds(999), 2000);
            Assert.Equal(1000, raw.Count);

            // 降采样查询 (将 1000 点压缩至 50 点用于极速图表绘制)
            var downsampled = await repo.QueryDownsampledAsync("Mixer.Power", startTime, startTime.AddSeconds(999), targetPointCount: 50);
            Assert.True(downsampled.Count <= 50);
            Assert.True(downsampled.Count >= 40);

            // 验证时序点单调递增
            for (int i = 1; i < downsampled.Count; i++)
            {
                Assert.True(downsampled[i].Time >= downsampled[i - 1].Time);
            }

            // 清理过期数据
            int cleaned = await repo.CleanupExpiredDataAsync(retentionDays: 30);
            Assert.True(cleaned >= 0);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task HistoryArchiveWorker_AutoFlush_Test()
    {
        var bus = new RealtimeDataBus();
        var tempDb = Path.Combine(Path.GetTempPath(), $"scada_worker_{Guid.NewGuid():N}.db");
        var repo = new SqliteHistoryRepository($"Data Source={tempDb}");

        await using var worker = new HistoryArchiveWorker(bus, repo)
        {
            FlushIntervalMs = 50 // 高频落盘测试
        };

        try
        {
            // 向总线推入 5 条时序数据
            for (int i = 0; i < 5; i++)
            {
                bus.PublishSnapshot(new TagValueSnapshot
                {
                    TagId = "Pump.Flow_Rate",
                    Value = 120.0 + i,
                    Quality = QualityCode.Good,
                    Timestamp = DateTime.Now
                });
            }

            // 显式刷盘
            await worker.FlushAsync();

            // 验证 SQLite 中已成功落盘
            var history = await repo.QueryRawHistoryAsync("Pump.Flow_Rate", DateTime.Now.AddMinutes(-5), DateTime.Now.AddMinutes(5));
            Assert.Equal(5, history.Count);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    #endregion

    #region Mock Helpers

    private class MockConfigurationService : IConfigurationService
    {
        private readonly List<ChannelConfig> _channels;
        private readonly List<DeviceNode> _devices;
        private readonly List<TagNode> _tags;

        public MockConfigurationService(ChannelConfig channel, DeviceNode device, IEnumerable<TagNode> tags)
        {
            _channels = new List<ChannelConfig> { channel };
            _devices = new List<DeviceNode> { device };
            _tags = tags.ToList();
        }

        public Task<ScadaConfigurationData> LoadConfigurationAsync() =>
            Task.FromResult(new ScadaConfigurationData(_channels, _devices, _tags));

        public Task SaveConfigurationAsync(IEnumerable<ChannelConfig> channels, IEnumerable<DeviceNode> devices, IEnumerable<TagNode> tags) => Task.CompletedTask;
        public ChannelConfig CreateChannel(ChannelType channelType, int existingChannelCount, string? preferredSerialPort = null) => new();
        public Task DeleteChannelAsync(string channelId) => Task.CompletedTask;
        public DeviceNode CreateDevice(string deviceId, string name, string channelId, ProtocolType protocolType, int stationAddress = 1, int pollIntervalMs = 100) => new();
        public Task<IReadOnlyList<DeviceNode>> GetDevicesAsync() => Task.FromResult<IReadOnlyList<DeviceNode>>(_devices);
        public Task DeleteDeviceAsync(string deviceId) => Task.CompletedTask;
        public Task<int> DeleteDeviceAsync(string deviceId, IEnumerable<TagNode> allTags) => Task.FromResult(0);
        public TagNode CreateTag(string tagId, string deviceId, string name, string address, TagDataType dataType, TagAccessMode accessMode = TagAccessMode.ReadOnly) => new();
        public TagNode CreateTag(string deviceId, int currentTagCountInDevice) => new();
        public Task DeleteTagAsync(string tagId) => Task.CompletedTask;
        public void CleanUnusedMediaParameters(ChannelConfig channel) { }
        public Task<IReadOnlyList<TagNode>> GetAllTagsAsync() => Task.FromResult<IReadOnlyList<TagNode>>(_tags);

        public Task<IReadOnlyList<UiViewConfig>> GetUiViewsAsync() => Task.FromResult<IReadOnlyList<UiViewConfig>>(Array.Empty<UiViewConfig>());
        public Task<UiViewConfig?> GetUiViewByIdAsync(string viewId) => Task.FromResult<UiViewConfig?>(null);
        public Task SaveUiViewAsync(UiViewConfig view) => Task.CompletedTask;
        public Task DeleteUiViewAsync(string viewId) => Task.CompletedTask;
        public UiViewConfig CreateDefaultUiView(string? name = null, string? deviceId = null) => new();
    }

    private class MockChannelFactory : IChannelFactory
    {
        public IChannel CreateChannel(ChannelConfig config) => new MockChannel(config.ChannelId);
    }

    private class MockChannel : IChannel
    {
        public string ChannelId { get; }
        public ChannelType ChannelType => ChannelType.TcpClient;
        public ChannelState State { get; private set; } = ChannelState.Connected;
        public bool IsOpen => State == ChannelState.Connected;

#pragma warning disable CS0067
        public event EventHandler<ChannelState>? StateChanged;
#pragma warning restore CS0067

        public MockChannel(string id) => ChannelId = id;
        public Task<bool> OpenAsync(CancellationToken ct = default) { State = ChannelState.Connected; return Task.FromResult(true); }
        public Task CloseAsync() { State = ChannelState.Disconnected; return Task.CompletedTask; }
        public Task<int> SendAsync(byte[] buffer, int offset, int count, CancellationToken ct = default) => Task.FromResult(count);
        public Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken ct = default) => Task.FromResult(0);
        public void ClearBuffer() { }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class MockDriverFactory : IDriverFactory
    {
        public IDriver CreateDriver(ProtocolType protocolType, string? customProtocolName = null) => new MockDriver();
        public IDriver CreateDriver(string protocolName) => new MockDriver();
        public IReadOnlyCollection<string> GetSupportedProtocols() => new[] { "MockDriver" };
    }

    private class MockDriver : IDriver
    {
        public ProtocolType ProtocolType => ProtocolType.ModbusTcp;
        public string ProtocolName => "MockDriver";

        public Task<bool> InitializeAsync(IChannel channel, DeviceNode device, CancellationToken ct = default) => Task.FromResult(true);
        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<IReadOnlyDictionary<string, TagValueSnapshot>> ReadBatchAsync(IEnumerable<TagNode> tags, CancellationToken ct = default)
        {
            var dict = new Dictionary<string, TagValueSnapshot>();
            foreach (var t in tags)
            {
                dict[t.TagId] = new TagValueSnapshot
                {
                    TagId = t.TagId,
                    Value = 100.0,
                    RawValue = 100,
                    Quality = QualityCode.Good,
                    Timestamp = DateTime.Now
                };
            }
            return Task.FromResult<IReadOnlyDictionary<string, TagValueSnapshot>>(dict);
        }

        public Task<WriteResult> WriteTagAsync(TagNode tag, object value, CancellationToken ct = default)
        {
            return Task.FromResult(WriteResult.Success(tag.TagId, value, 10));
        }

        public Task<IReadOnlyDictionary<string, WriteResult>> WriteBatchAsync(IEnumerable<KeyValuePair<TagNode, object>> writes, CancellationToken ct = default)
        {
            var dict = new Dictionary<string, WriteResult>();
            if (writes != null)
            {
                foreach (var w in writes)
                {
                    dict[w.Key.TagId] = WriteResult.Success(w.Key.TagId, w.Value, 10);
                }
            }
            return Task.FromResult<IReadOnlyDictionary<string, WriteResult>>(dict);
        }

        public void Dispose() { }
    }

    #endregion
}
