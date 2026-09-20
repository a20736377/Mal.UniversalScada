using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 历史时序数据持久化仓储接口。
/// 隔离底层具体时序数据库实现（DuckDB / TDengine / TimescaleDB / SQLite分表）。
/// 核心提供高吞吐批量入库能力与基于降采样（Downsampling）的高性能图表查询能力。
/// </summary>
public interface IHistoryRepository
{
    /// <summary>
    /// 批量高速持久化历史采样记录
    /// </summary>
    /// <param name="records">待写入的历史记录集合</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>实际写入的记录条数</returns>
    Task<int> InsertBatchAsync(IEnumerable<TagHistoryRecord> records, CancellationToken ct = default);

    /// <summary>
    /// 高性能降采样历史查询（专门为趋势图表秒级渲染优化）。
    /// 无论底层原始点数是 10 万还是 1000 万，均在数据库端或内存引擎中通过 LTTB 等算法压缩至目标像素数量（如 1000~2000 个点），
    /// 极大降低网络传输开销与 UI 渲染负担。
    /// </summary>
    /// <param name="tagId">点位自增 ID</param>
    /// <param name="startTime">查询起始时间 (包含)</param>
    /// <param name="endTime">查询结束时间 (包含)</param>
    /// <param name="targetPointCount">目标期望返回的点数量 (通常传图表控件的像素宽度或固定 1000~2000)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>降采样后的时序数据点列表</returns>
    Task<IReadOnlyList<TimeSeriesPoint>> QueryDownsampledAsync(
        long tagId, 
        DateTime startTime, 
        DateTime endTime, 
        int targetPointCount = 1000, 
        CancellationToken ct = default);

    /// <summary>
    /// 查询原始历史记录（主要用于报表导出、Excel导出或短周期精密分析）
    /// </summary>
    /// <param name="tagId">点位自增 ID</param>
    /// <param name="startTime">起始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="maxRecords">最大返回条数保护限制</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>原始历史记录列表</returns>
    Task<IReadOnlyList<TagHistoryRecord>> QueryRawHistoryAsync(
        long tagId, 
        DateTime startTime, 
        DateTime endTime, 
        int maxRecords = 50000, 
        CancellationToken ct = default);

    /// <summary>
    /// 按照保留周期（Retention Policy）清理指定天数之前的过期历史数据
    /// </summary>
    /// <param name="retentionDays">保留天数 (如 90 天)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>清理释放的记录总数</returns>
    Task<int> CleanupExpiredDataAsync(int retentionDays, CancellationToken ct = default);
}
