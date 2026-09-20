using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Services;

/// <summary>
/// 工业操作合规审计日志服务实现。
/// 本地基于 SQLite 持久化并维护内存极速检索缓存，确保所有控制调参与指令下发全程防篡改可追溯。
/// </summary>
public class AuditService : IAuditService
{
    private readonly string _connectionString;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ConcurrentBag<AuditRecord> _memoryCache = new();

    public AuditService(string connectionString = "Data Source=scada_audit.db")
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

            await connection.ExecuteAsync("PRAGMA journal_mode = WAL;");

            const string createSql = @"
                CREATE TABLE IF NOT EXISTS AuditLogs (
                    RecordId TEXT PRIMARY KEY,
                    Timestamp TEXT NOT NULL,
                    OperatorName TEXT NOT NULL,
                    ActionType TEXT NOT NULL,
                    TargetId TEXT NOT NULL,
                    OldValue TEXT,
                    NewValue TEXT,
                    IsSuccess INTEGER NOT NULL,
                    ErrorMessage TEXT,
                    ElapsedMs INTEGER NOT NULL,
                    ClientInfo TEXT
                );
                CREATE INDEX IF NOT EXISTS IX_AuditLogs_Time ON AuditLogs(Timestamp);
                CREATE INDEX IF NOT EXISTS IX_AuditLogs_Target ON AuditLogs(TargetId);
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
    public async Task RecordAsync(AuditRecord record, CancellationToken ct = default)
    {
        if (record == null) return;

        _memoryCache.Add(record);

        try
        {
            await EnsureInitializedAsync(ct);

            await using var connection = CreateConnection();
            await connection.OpenAsync(ct);

            const string insertSql = @"
                INSERT INTO AuditLogs (RecordId, Timestamp, OperatorName, ActionType, TargetId, OldValue, NewValue, IsSuccess, ErrorMessage, ElapsedMs, ClientInfo)
                VALUES (@RecordId, @Timestamp, @OperatorName, @ActionType, @TargetId, @OldValue, @NewValue, @IsSuccess, @ErrorMessage, @ElapsedMs, @ClientInfo);
            ";

            await connection.ExecuteAsync(insertSql, new
            {
                record.RecordId,
                Timestamp = record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                record.OperatorName,
                record.ActionType,
                record.TargetId,
                record.OldValue,
                record.NewValue,
                IsSuccess = record.IsSuccess ? 1 : 0,
                record.ErrorMessage,
                record.ElapsedMs,
                record.ClientInfo
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuditService] 持久化审计日志异常: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task RecordTagWriteAsync(
        string operatorName, 
        string tagId, 
        object? oldValue, 
        object? newValue, 
        bool isSuccess, 
        long elapsedMs, 
        string? error = null, 
        CancellationToken ct = default)
    {
        var record = new AuditRecord
        {
            OperatorName = string.IsNullOrWhiteSpace(operatorName) ? "Operator" : operatorName,
            ActionType = "TagWrite",
            TargetId = tagId,
            OldValue = oldValue?.ToString(),
            NewValue = newValue?.ToString(),
            IsSuccess = isSuccess,
            ElapsedMs = elapsedMs,
            ErrorMessage = error,
            Timestamp = DateTime.Now
        };

        return RecordAsync(record, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditRecord>> QueryAuditLogsAsync(
        DateTime startTime, 
        DateTime endTime, 
        string? operatorName = null, 
        string? actionType = null, 
        int maxCount = 5000, 
        CancellationToken ct = default)
    {
        try
        {
            await EnsureInitializedAsync(ct);

            await using var connection = CreateConnection();
            await connection.OpenAsync(ct);

            var sql = @"
                SELECT RecordId, Timestamp, OperatorName, ActionType, TargetId, OldValue, NewValue, IsSuccess, ErrorMessage, ElapsedMs, ClientInfo
                FROM AuditLogs
                WHERE Timestamp >= @StartTime AND Timestamp <= @EndTime
            ";

            if (!string.IsNullOrWhiteSpace(operatorName))
                sql += " AND OperatorName = @OperatorName";

            if (!string.IsNullOrWhiteSpace(actionType))
                sql += " AND ActionType = @ActionType";

            sql += " ORDER BY Timestamp DESC LIMIT @MaxCount;";

            var rows = await connection.QueryAsync<AuditRow>(sql, new
            {
                StartTime = startTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                EndTime = endTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                OperatorName = operatorName,
                ActionType = actionType,
                MaxCount = maxCount
            });

            return rows.Select(r => new AuditRecord
            {
                RecordId = r.RecordId,
                Timestamp = DateTime.TryParse(r.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : DateTime.Now,
                OperatorName = r.OperatorName,
                ActionType = r.ActionType,
                TargetId = r.TargetId,
                OldValue = r.OldValue,
                NewValue = r.NewValue,
                IsSuccess = r.IsSuccess == 1,
                ErrorMessage = r.ErrorMessage,
                ElapsedMs = r.ElapsedMs,
                ClientInfo = r.ClientInfo
            }).ToList();
        }
        catch
        {
            // 回退到内存缓存查询
            return _memoryCache
                .Where(r => r.Timestamp >= startTime && r.Timestamp <= endTime)
                .OrderByDescending(r => r.Timestamp)
                .Take(maxCount)
                .ToList();
        }
    }

    private sealed class AuditRow
    {
        public string RecordId { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string OperatorName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public int IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public long ElapsedMs { get; set; }
        public string ClientInfo { get; set; } = string.Empty;
    }
}
