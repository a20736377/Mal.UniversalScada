using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Configuration;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Services;

/// <summary>
/// 工业 SCADA 读写双优先级调度引擎（Priority Scheduler）核心实现。
/// 保障控制写入指令享有绝对最高优先级即时插队执行，并协调多物理通道与下位机设备的并发周期性采集。
/// </summary>
public class PriorityScheduler : IPriorityScheduler
{
    private readonly IConfigurationService _configService;
    private readonly IChannelFactory _channelFactory;
    private readonly IDriverFactory _driverFactory;
    private readonly IRealtimeDataBus _dataBus;

    private readonly ConcurrentDictionary<string, IChannel> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IDriver> _drivers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TagNode> _tagLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DeviceNode> _deviceLookup = new(StringComparer.OrdinalIgnoreCase);

    // 高优写指令异步通道 (Key: ChannelId)
    private readonly ConcurrentDictionary<string, Channel<WriteJob>> _writeChannels = new(StringComparer.OrdinalIgnoreCase);

    // 通道调度工作线程/任务集合
    private readonly List<Task> _workerTasks = new();
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private readonly object _stateLock = new();

    public bool IsRunning => _isRunning;

    public PriorityScheduler(
        IConfigurationService configService,
        IChannelFactory channelFactory,
        IDriverFactory driverFactory,
        IRealtimeDataBus dataBus)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _channelFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
        _driverFactory = driverFactory ?? throw new ArgumentNullException(nameof(driverFactory));
        _dataBus = dataBus ?? throw new ArgumentNullException(nameof(dataBus));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();
        }

        // 1. 装载工程拓扑：通道、设备与测点
        var config = await _configService.LoadConfigurationAsync();
        var channelNodes = config.Channels;
        var deviceNodes = config.Devices;
        var tagNodes = config.Tags;

        _tagLookup.Clear();
        foreach (var tag in tagNodes)
        {
            _tagLookup[tag.TagId] = tag;
            if (_dataBus is RealtimeDataBus rdb)
            {
                rdb.RegisterTag(tag);
            }
        }

        _deviceLookup.Clear();
        foreach (var dev in deviceNodes)
        {
            _deviceLookup[dev.DeviceId] = dev;
        }

        // 2. 为每个通道建立连接、写队列与独立轮询工作线程
        _workerTasks.Clear();
        var linkedToken = _cts.Token;

        foreach (var chNode in channelNodes.Where(c => c.IsEnabled))
        {
            try
            {
                var channel = _channelFactory.CreateChannel(chNode);
                _channels[chNode.ChannelId] = channel;

                // 准备该通道的高优写通道
                var writeChan = Channel.CreateUnbounded<WriteJob>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });
                _writeChannels[chNode.ChannelId] = writeChan;

                // 挂载在该通道下的有效设备
                var channelDevices = deviceNodes.Where(d => d.IsEnabled && string.Equals(d.ChannelId, chNode.ChannelId, StringComparison.OrdinalIgnoreCase)).ToList();

                // 实例化设备协议驱动并初始化
                foreach (var dev in channelDevices)
                {
                    var driver = _driverFactory.CreateDriver(dev.ProtocolType, dev.CustomProtocolName);
                    try
                    {
                        await driver.InitializeAsync(channel, dev, linkedToken);
                    }
                    catch { }
                    _drivers[dev.DeviceId] = driver;
                }

                // 启动针对该通道的独立调度循环
                var workerTask = Task.Run(() => ChannelWorkerLoopAsync(chNode, channel, writeChan, channelDevices, linkedToken), linkedToken);
                _workerTasks.Add(workerTask);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PriorityScheduler] 启动通道 [{chNode.ChannelId}] 失败: {ex.Message}");
            }
        }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        lock (_stateLock)
        {
            if (!_isRunning) return;
            _isRunning = false;
            cts = _cts;
            _cts = null;
        }

        if (cts != null)
        {
            cts.Cancel();
            try
            {
                await Task.WhenAll(_workerTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                cts.Dispose();
            }
        }

        // 关闭所有通信通道
        foreach (var ch in _channels.Values)
        {
            try { await ch.CloseAsync(); } catch { }
        }

        _channels.Clear();
        _drivers.Clear();
        _writeChannels.Clear();
        _workerTasks.Clear();
    }

    /// <inheritdoc />
    public async Task<WriteResult> EnqueueWriteAsync(string tagId, object value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tagId))
            return WriteResult.Failed(tagId, value, "点位 ID 不能为空。");

        if (!_tagLookup.TryGetValue(tagId, out var tag))
            return WriteResult.Failed(tagId, value, $"未找到点位 [{tagId}] 的元数据定义。");

        if (!_deviceLookup.TryGetValue(tag.DeviceId, out var device))
            return WriteResult.Failed(tagId, value, $"未找到点位 [{tagId}] 所属设备 [{tag.DeviceId}]。");

        if (!_writeChannels.TryGetValue(device.ChannelId, out var writeChan))
        {
            // 如果系统尚未启动或通道未就绪，直接尝试单次反射写入或返回失败
            return WriteResult.Failed(tagId, value, $"通道 [{device.ChannelId}] 未处于激活调度状态。");
        }

        var tcs = new TaskCompletionSource<WriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = new WriteJob(tag, device, value, tcs, ct);

        // 写入队列插队
        await writeChan.Writer.WriteAsync(job, ct);

        // 等待执行并返回结果
        return await tcs.Task;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, WriteResult>> EnqueueBatchWriteAsync(
        IEnumerable<KeyValuePair<string, object>> writes, 
        CancellationToken ct = default)
    {
        var resultDict = new Dictionary<string, WriteResult>();
        if (writes == null) return resultDict;

        var tasks = writes.Select(async kvp =>
        {
            var res = await EnqueueWriteAsync(kvp.Key, kvp.Value, ct);
            return new KeyValuePair<string, WriteResult>(kvp.Key, res);
        });

        var results = await Task.WhenAll(tasks);
        foreach (var r in results)
        {
            resultDict[r.Key] = r.Value;
        }

        return resultDict;
    }

    /// <inheritdoc />
    public Task TriggerImmediatePollAsync(string deviceId)
    {
        // 即时轮询标记 (在轮询循环中重置时间戳)
        if (_deviceLookup.TryGetValue(deviceId, out var dev) &&
            _deviceLastPoll.ContainsKey(deviceId))
        {
            _deviceLastPoll[deviceId] = DateTime.MinValue; // 设为过去时间，促使其在下一轮立即执行
        }
        return Task.CompletedTask;
    }

    private readonly ConcurrentDictionary<string, DateTime> _deviceLastPoll = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 单个通信通道的工作调度循环。
    /// 核心保障：每次轮询前均优先清空高优写指令队列！
    /// </summary>
    private async Task ChannelWorkerLoopAsync(
        ChannelConfig chNode, 
        IChannel channel, 
        Channel<WriteJob> writeChan, 
        List<DeviceNode> devices, 
        CancellationToken ct)
    {
        // 自动连接链路
        try
        {
            if (channel.State != ChannelState.Connected)
            {
                await channel.OpenAsync(ct);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PriorityScheduler] 打开通道 [{chNode.ChannelId}] 失败: {ex.Message}");
        }

        // 初始化设备轮询时戳
        foreach (var dev in devices)
        {
            _deviceLastPoll[dev.DeviceId] = DateTime.MinValue;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // ==========================================
                // 1. 最高优先级：检查并排空写指令队列
                // ==========================================
                while (writeChan.Reader.TryRead(out var writeJob))
                {
                    await ProcessWriteJobAsync(channel, writeJob);
                }

                // ==========================================
                // 2. 次高优先级：设备周期采集轮询
                // ==========================================
                var now = DateTime.Now;
                foreach (var dev in devices)
                {
                    if (ct.IsCancellationRequested) break;

                    // 再次检查是否有突发的写指令插队
                    if (writeChan.Reader.TryRead(out var emergencyWrite))
                    {
                        await ProcessWriteJobAsync(channel, emergencyWrite);
                    }

                    // 检查轮询间隔
                    int pollInterval = dev.DefaultPollIntervalMs > 0 ? dev.DefaultPollIntervalMs : 100;
                    if (!_deviceLastPoll.TryGetValue(dev.DeviceId, out var lastPoll) || 
                        (now - lastPoll).TotalMilliseconds >= pollInterval)
                    {
                        _deviceLastPoll[dev.DeviceId] = now;
                        await PollDeviceTagsAsync(channel, dev, ct);
                    }
                }

                // 小休眠微时间片，降低 CPU 空转，同时保持极高写响应
                await Task.Delay(10, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PriorityScheduler] 通道 [{chNode.ChannelId}] 调度异常: {ex.Message}");
                await Task.Delay(100, ct);
            }
        }
    }

    /// <summary>
    /// 处理单条高优控制写指令
    /// </summary>
    private async Task ProcessWriteJobAsync(IChannel channel, WriteJob job)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!_drivers.TryGetValue(job.Device.DeviceId, out var driver))
            {
                job.Tcs.TrySetResult(WriteResult.Failed(job.Tag.TagId, job.Value, "未找到关联协议驱动。"));
                return;
            }

            // 确保通道连通
            if (channel.State != ChannelState.Connected)
            {
                await channel.OpenAsync(job.Ct);
            }

            // 执行驱动写入操作
            var writeResult = await driver.WriteTagAsync(job.Tag, job.Value, job.Ct);
            sw.Stop();

            if (writeResult.IsSuccess)
            {
                // 回写总线内存快照，保持状态一致
                _dataBus.PublishSnapshot(new TagValueSnapshot
                {
                    TagId = job.Tag.TagId,
                    Value = job.Value,
                    RawValue = job.Value,
                    Quality = QualityCode.Good,
                    Timestamp = DateTime.Now
                });
            }

            job.Tcs.TrySetResult(writeResult);
        }
        catch (Exception ex)
        {
            sw.Stop();
            job.Tcs.TrySetResult(WriteResult.Failed(job.Tag.TagId, job.Value, ex.Message, sw.ElapsedMilliseconds));
        }
    }

    /// <summary>
    /// 执行指定设备的周期点位批量读取
    /// </summary>
    private async Task PollDeviceTagsAsync(IChannel channel, DeviceNode device, CancellationToken ct)
    {
        if (!_drivers.TryGetValue(device.DeviceId, out var driver)) return;

        var tags = _tagLookup.Values.Where(t => string.Equals(t.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase)).ToList();
        if (tags.Count == 0) return;

        try
        {
            if (channel.State != ChannelState.Connected)
            {
                await channel.OpenAsync(ct);
            }

            // 执行批量采集
            var snapshotDict = await driver.ReadBatchAsync(tags, ct);
            if (snapshotDict != null && snapshotDict.Count > 0)
            {
                _dataBus.PublishSnapshots(snapshotDict.Values);
            }
        }
        catch (Exception ex)
        {
            // 通信异常：将该设备所有点位发布为 CommFailure
            var failureList = tags.Select(t => new TagValueSnapshot
            {
                TagId = t.TagId,
                Value = null,
                RawValue = null,
                Quality = QualityCode.CommFailure,
                Timestamp = DateTime.Now
            });
            _dataBus.PublishSnapshots(failureList);
            Debug.WriteLine($"[PriorityScheduler] 采集设备 [{device.DeviceId}] 失败: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }

    private sealed record WriteJob(
        TagNode Tag, 
        DeviceNode Device, 
        object Value, 
        TaskCompletionSource<WriteResult> Tcs, 
        CancellationToken Ct);
}
