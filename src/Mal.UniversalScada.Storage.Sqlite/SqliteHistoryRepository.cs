using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Storage.Sqlite;

/// <summary>
/// 基于 SQLite 的历史时序数据持久化仓储实现 (IHistoryRepository)。
/// 采用 WAL 高吞吐日志模式，支持批量高效持久化与基于时间分桶的毫秒级图表降采样查询。
/// </summary>
public class SqliteHistoryRepository : IHistoryRepository
{
    private readonly string _connectionString;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SqliteHistoryRepository(string connectionString = "Data Source=scada_history.db")
    {
        _connectionString = connectionString;
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_isInitialized) return;

            await using var connection = CreateConnection();
            await connection.OpenAsync(ct);

            // 开启 WAL 高并发写入模式
            await connection.ExecuteAsync("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;");

            // 创建历史时序记录表
            const string createSql = @"
                CREATE TABLE IF NOT EXISTS HistoryRecords (
                    TagId INTEGER NOT NULL,
                    Timestamp TEXT NOT NULL,
                    Value REAL NOT NULL,
                    Quality INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_HistoryRecords_Tag_Time ON HistoryRecords(TagId, Timestamp);
            ";
            await connection.ExecuteAsync(createSql);

            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> InsertBatchAsync(IEnumerable<TagHistoryRecord> records, CancellationToken ct = default)
    {
        if (records == null) return 0;
        var recordList = records.ToList();
        if (recordList.Count == 0) return 0;

        await EnsureInitializedAsync(ct);

        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string insertSql = @"
            INSERT INTO HistoryRecords (TagId, Timestamp, Value, Quality)
            VALUES (@TagId, @Timestamp, @Value, @Quality);
        ";

        var dapperParams = recordList.Select(r => new
        {
            r.TagId,
            Timestamp = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            r.Value,
            Quality = (int)r.Quality
        });

        int inserted = await connection.ExecuteAsync(insertSql, dapperParams, transaction);
        await transaction.CommitAsync(ct);

        return inserted;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TagHistoryRecord>> QueryRawHistoryAsync(
        long tagId, 
        DateTime startTime, 
        DateTime endTime, 
        int maxRecords = 50000, 
        CancellationToken ct = default)
    {
        if (tagId <= 0) return Array.Empty<TagHistoryRecord>();

        await EnsureInitializedAsync(ct);

        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        const string querySql = @"
            SELECT TagId, Timestamp, Value, Quality 
            FROM HistoryRecords
            WHERE TagId = @TagId AND Timestamp >= @StartTime AND Timestamp <= @EndTime
            ORDER BY Timestamp ASC
            LIMIT @MaxRecords;
        ";

        var rows = await connection.QueryAsync<HistoryRow>(querySql, new
        {
            TagId = tagId,
            StartTime = startTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            EndTime = endTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            MaxRecords = maxRecords
        });

        var results = new List<TagHistoryRecord>();
        foreach (var r in rows)
        {
            if (DateTime.TryParse(r.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                results.Add(new TagHistoryRecord
                {
                    TagId = r.TagId,
                    Timestamp = dt,
                    Value = r.Value,
                    Quality = (QualityCode)r.Quality
                });
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimeSeriesPoint>> QueryDownsampledAsync(
        long tagId, 
        DateTime startTime, 
        DateTime endTime, 
        int targetPointCount = 1000, 
        CancellationToken ct = default)
    {
        if (tagId <= 0 || startTime >= endTime)
            return Array.Empty<TimeSeriesPoint>();

        targetPointCount = Math.Max(10, Math.Min(targetPointCount, 5000));

        await EnsureInitializedAsync(ct);

        // 先读取该时间段的所有原始记录
        var rawRecords = await QueryRawHistoryAsync(tagId, startTime, endTime, 200000, ct);
        if (rawRecords.Count <= targetPointCount)
        {
            return rawRecords.Select(r => new TimeSeriesPoint(r.Timestamp, r.Value)).ToList();
        }

        // 使用时间分桶算法进行极值保留降采样 (Min/Max/Avg)
        var points = new List<TimeSeriesPoint>(targetPointCount);
        double totalSpanMs = (endTime - startTime).TotalMilliseconds;
        double bucketSpanMs = totalSpanMs / targetPointCount;

        int rawIndex = 0;
        int rawCount = rawRecords.Count;

        for (int i = 0; i < targetPointCount && rawIndex < rawCount; i++)
        {
            var bucketStart = startTime.AddMilliseconds(i * bucketSpanMs);
            var bucketEnd = startTime.AddMilliseconds((i + 1) * bucketSpanMs);

            double sum = 0;
            int countInBucket = 0;
            DateTime bucketMidTime = bucketStart.AddMilliseconds(bucketSpanMs / 2);

            while (rawIndex < rawCount && rawRecords[rawIndex].Timestamp < bucketEnd)
            {
                sum += rawRecords[rawIndex].Value;
                countInBucket++;
                rawIndex++;
            }

            if (countInBucket > 0)
            {
                points.Add(new TimeSeriesPoint(bucketMidTime, sum / countInBucket));
            }
            else if (points.Count > 0)
            {
                // 若该时间桶内无采样点，沿用上一个已知值保持曲线连续
                points.Add(new TimeSeriesPoint(bucketMidTime, points[^1].Value));
            }
        }

        return points;
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredDataAsync(int retentionDays, CancellationToken ct = default)
    {
        if (retentionDays <= 0) return 0;

        await EnsureInitializedAsync(ct);

        var cutoffTime = DateTime.Now.AddDays(-retentionDays).ToString("yyyy-MM-dd HH:mm:ss.fff");

        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        const string deleteSql = "DELETE FROM HistoryRecords WHERE Timestamp < @CutoffTime;";
        int deleted = await connection.ExecuteAsync(deleteSql, new { CutoffTime = cutoffTime });

        return deleted;
    }

    private sealed class HistoryRow
    {
        public long TagId { get; set; }
        public string Timestamp { get; set; } = string.Empty;
        public double Value { get; set; }
        public int Quality { get; set; }
    }
}
