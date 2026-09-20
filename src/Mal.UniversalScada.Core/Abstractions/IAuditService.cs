using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 操作合规审计日志服务接口。
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// 记录一条关键操作审计日志
    /// </summary>
    Task RecordAsync(AuditRecord record, CancellationToken ct = default);

    /// <summary>
    /// 便捷记录点位写入操作审计
    /// </summary>
    Task RecordTagWriteAsync(
        string operatorName, 
        long tagId, 
        object? oldValue, 
        object? newValue, 
        bool isSuccess, 
        long elapsedMs, 
        string? error = null, 
        CancellationToken ct = default);

    /// <summary>
    /// 便捷记录点位写入操作审计
    /// </summary>
    Task RecordTagWriteAsync(
        string operatorName, 
        string tagId, 
        object? oldValue, 
        object? newValue, 
        bool isSuccess, 
        long elapsedMs, 
        string? error = null, 
        CancellationToken ct = default);

    /// <summary>
    /// 多条件查询历史审计日志
    /// </summary>
    Task<IReadOnlyList<AuditRecord>> QueryAuditLogsAsync(
        DateTime startTime, 
        DateTime endTime, 
        string? operatorName = null, 
        string? actionType = null, 
        int maxCount = 5000, 
        CancellationToken ct = default);
}
