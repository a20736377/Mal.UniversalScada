using Microsoft.Extensions.DependencyInjection;
using Mal.UniversalScada.Core.Abstractions;

namespace Mal.UniversalScada.Storage.Sqlite;

/// <summary>
/// SQLite 存储服务依赖注入扩展方法
/// </summary>
public static class SqliteStorageExtensions
{
    /// <summary>
    /// 注册基于 SQLite 的组态配置仓储服务 (IConfigRepository)
    /// </summary>
    /// <param name="services">DI 服务容器</param>
    /// <param name="connectionString">SQLite 数据库连接字符串 (默认使用本地 scada_config.db)</param>
    /// <returns>服务容器本身</returns>
    public static IServiceCollection AddSqliteConfigStorage(
        this IServiceCollection services, 
        string connectionString = "Data Source=scada_config.db")
    {
        services.AddSingleton<IConfigRepository>(_ => new SqliteConfigRepository(connectionString));
        services.AddSingleton<IUserRepository>(_ => new SqliteUserRepository(connectionString));
        return services;
    }
}
