using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Services;

/// <summary>
/// 历史时序数据后台归档工作者 (History Archive Worker)。
/// 持续订阅实时数据总线，维护内存并发批处理缓冲池，采用双阈值定时/定量触发异步批量写入持久化仓储，
/// 显著降低高频采集下的数据库 I/O 压力与磁盘碎片。
/// </summary>
public class HistoryArchiveWorker : IAsyncDisposable
{
    private readonly IRealtimeDataBus _dataBus;
    private readonly IHistoryRepository _historyRepo;
    private readonly IDisposable _busSubscription;

    private readonly ConcurrentQueue<TagHistoryRecord> _bufferQueue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _workerTask;

    public int BatchSizeThreshold { get; set; } = 500;
    public int FlushIntervalMs { get; set; } = 1000;
    public int BufferedCount => _bufferQueue.Count;

    public HistoryArchiveWorker(IRealtimeDataBus dataBus, IHistoryRepository historyRepo)
    {
        _dataBus = dataBus ?? throw new ArgumentNullException(nameof(dataBus));
        _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));

        _busSubscription = _dataBus.SubscribeAll(OnSnapshotReceived);
        _workerTask = Task.Run(WorkerLoopAsync);
    }

    private void OnSnapshotReceived(TagValueSnapshot snapshot)
    {
        if (snapshot == null || snapshot.TagId <= 0) return;

        // 仅浮点或能够换算为双精度浮点数的模拟量进行归档
        if (snapshot.Value != null)
        {
            try
            {
                double num = Convert.ToDouble(snapshot.Value);
                _bufferQueue.Enqueue(new TagHistoryRecord
                {
                    TagId = snapshot.TagId,
                    Timestamp = snapshot.Timestamp,
                    Value = num,
                    Quality = snapshot.Quality
                });
            }
            catch { }
        }
    }

    private async Task WorkerLoopAsync()
    {
        var token = _cts.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(FlushIntervalMs, token);
                await FlushInternalAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HistoryArchiveWorker] 归档异常: {ex.Message}");
            }
        }

        // 退出前做最终刷盘排空
        await FlushInternalAsync(CancellationToken.None);
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        await FlushInternalAsync(ct);
    }

    private async Task FlushInternalAsync(CancellationToken ct)
    {
        if (_bufferQueue.IsEmpty) return;

        var batch = new List<TagHistoryRecord>();
        while (_bufferQueue.TryDequeue(out var record))
        {
            batch.Add(record);
            if (batch.Count >= 5000) // 单次单批上限保护
                break;
        }

        if (batch.Count > 0)
        {
            try
            {
                await _historyRepo.InsertBatchAsync(batch, ct);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HistoryArchiveWorker] 批量落盘失败: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _busSubscription.Dispose();
        _cts.Cancel();
        try
        {
            await _workerTask;
        }
        catch { }
        finally
        {
            _cts.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
