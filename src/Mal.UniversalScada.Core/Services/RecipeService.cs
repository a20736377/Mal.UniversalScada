using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Services;

/// <summary>
/// 工业工艺配方管理服务实现 (IRecipeService)。
/// 负责产线参数配方的持久化维护，并基于优先级调度引擎执行一键批量高速下发与下位机状态回写校验。
/// </summary>
public class RecipeService : IRecipeService
{
    private readonly IPriorityScheduler _scheduler;
    private readonly IAuditService? _auditService;
    private readonly string _connectionString;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ConcurrentDictionary<string, RecipeModel> _memoryRecipes = new(StringComparer.OrdinalIgnoreCase);

    public RecipeService(
        IPriorityScheduler scheduler, 
        IAuditService? auditService = null, 
        string connectionString = "Data Source=scada_recipes.db")
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _auditService = auditService;
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
                CREATE TABLE IF NOT EXISTS Recipes (
                    RecipeId TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    TargetDeviceId TEXT NOT NULL,
                    Version TEXT NOT NULL,
                    ItemsJson TEXT NOT NULL,
                    UpdatedTime TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_Recipes_Device ON Recipes(TargetDeviceId);
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
    public async Task<IReadOnlyList<RecipeModel>> GetRecipesByDeviceAsync(string deviceId)
    {
        try
        {
            await EnsureInitializedAsync(CancellationToken.None);

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            const string querySql = @"
                SELECT RecipeId, Name, TargetDeviceId, Version, ItemsJson, UpdatedTime
                FROM Recipes
                WHERE TargetDeviceId = @TargetDeviceId
                ORDER BY UpdatedTime DESC;
            ";

            var rows = await connection.QueryAsync<RecipeRow>(querySql, new { TargetDeviceId = deviceId });
            var list = new List<RecipeModel>();

            foreach (var r in rows)
            {
                var items = JsonSerializer.Deserialize<List<RecipeItem>>(r.ItemsJson) ?? new List<RecipeItem>();
                list.Add(new RecipeModel
                {
                    RecipeId = r.RecipeId,
                    Name = r.Name,
                    TargetDeviceId = r.TargetDeviceId,
                    Version = r.Version,
                    Items = items,
                    UpdatedTime = DateTime.TryParse(r.UpdatedTime, out var dt) ? dt : DateTime.Now
                });
            }

            return list;
        }
        catch
        {
            return _memoryRecipes.Values
                .Where(r => string.Equals(r.TargetDeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <inheritdoc />
    public async Task SaveRecipeAsync(RecipeModel recipe)
    {
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.RecipeId)) return;

        recipe.UpdatedTime = DateTime.Now;
        _memoryRecipes[recipe.RecipeId] = recipe;

        try
        {
            await EnsureInitializedAsync(CancellationToken.None);

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            const string upsertSql = @"
                INSERT INTO Recipes (RecipeId, Name, TargetDeviceId, Version, ItemsJson, UpdatedTime)
                VALUES (@RecipeId, @Name, @TargetDeviceId, @Version, @ItemsJson, @UpdatedTime)
                ON CONFLICT(RecipeId) DO UPDATE SET
                    Name = excluded.Name,
                    TargetDeviceId = excluded.TargetDeviceId,
                    Version = excluded.Version,
                    ItemsJson = excluded.ItemsJson,
                    UpdatedTime = excluded.UpdatedTime;
            ";

            var json = JsonSerializer.Serialize(recipe.Items);

            await connection.ExecuteAsync(upsertSql, new
            {
                recipe.RecipeId,
                recipe.Name,
                recipe.TargetDeviceId,
                recipe.Version,
                ItemsJson = json,
                UpdatedTime = recipe.UpdatedTime.ToString("yyyy-MM-dd HH:mm:ss.fff")
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RecipeService] 保存配方异常: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task DeleteRecipeAsync(string recipeId)
    {
        if (string.IsNullOrWhiteSpace(recipeId)) return;

        _memoryRecipes.TryRemove(recipeId, out _);

        try
        {
            await EnsureInitializedAsync(CancellationToken.None);

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await connection.ExecuteAsync("DELETE FROM Recipes WHERE RecipeId = @RecipeId;", new { RecipeId = recipeId });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RecipeService] 删除配方异常: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<RecipeApplyResult> ApplyRecipeToDeviceAsync(string recipeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipeId))
            return new RecipeApplyResult(false, "配方 ID 不能为空。", new Dictionary<long, WriteResult>());

        RecipeModel? recipe = null;
        if (!_memoryRecipes.TryGetValue(recipeId, out recipe))
        {
            try
            {
                await EnsureInitializedAsync(ct);
                await using var connection = CreateConnection();
                await connection.OpenAsync(ct);

                var r = await connection.QueryFirstOrDefaultAsync<RecipeRow>(
                    "SELECT RecipeId, Name, TargetDeviceId, Version, ItemsJson, UpdatedTime FROM Recipes WHERE RecipeId = @RecipeId;",
                    new { RecipeId = recipeId });

                if (r != null)
                {
                    var items = JsonSerializer.Deserialize<List<RecipeItem>>(r.ItemsJson) ?? new List<RecipeItem>();
                    recipe = new RecipeModel
                    {
                        RecipeId = r.RecipeId,
                        Name = r.Name,
                        TargetDeviceId = r.TargetDeviceId,
                        Version = r.Version,
                        Items = items
                    };
                }
            }
            catch { }
        }

        if (recipe == null)
            return new RecipeApplyResult(false, $"未找到 ID 为 [{recipeId}] 的配方数据。", new Dictionary<long, WriteResult>());

        if (recipe.Items.Count == 0)
            return new RecipeApplyResult(true, $"配方【{recipe.Name}】参数列表为空，无需下发。", new Dictionary<long, WriteResult>());

        var sw = Stopwatch.StartNew();

        // 组织待写入集合并提交调度器批量高优插队执行
        var writePairs = recipe.Items.Select(item => new KeyValuePair<long, object>(item.TagId, item.TargetValue));
        var writeResults = await _scheduler.EnqueueBatchWriteAsync(writePairs, ct);

        sw.Stop();

        int failCount = writeResults.Values.Count(r => !r.IsSuccess);
        bool isAllSuccess = failCount == 0;
        string message = isAllSuccess
            ? $"配方【{recipe.Name}】下发成功！共同步 {writeResults.Count} 项参数，耗时 {sw.ElapsedMilliseconds} ms。"
            : $"配方【{recipe.Name}】部分下发失败：{writeResults.Count - failCount} 项成功，{failCount} 项失败。";

        // 记录操作审计
        if (_auditService != null)
        {
            await _auditService.RecordAsync(new AuditRecord
            {
                OperatorName = "Operator",
                ActionType = "RecipeApply",
                TargetId = recipe.Name,
                OldValue = recipe.RecipeId,
                NewValue = $"{writeResults.Count} 项参数",
                IsSuccess = isAllSuccess,
                ErrorMessage = isAllSuccess ? null : message,
                ElapsedMs = sw.ElapsedMilliseconds
            }, ct);
        }

        return new RecipeApplyResult(isAllSuccess, message, writeResults);
    }

    private sealed class RecipeRow
    {
        public string RecipeId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TargetDeviceId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string ItemsJson { get; set; } = string.Empty;
        public string UpdatedTime { get; set; } = string.Empty;
    }
}
