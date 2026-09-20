using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Mal.UniversalScada.Core.Abstractions;

namespace Mal.UniversalScada.Storage.Sqlite;

/// <summary>
/// 全局 SCADA 数据库定位器，保证组态设计器 (Configurator) 与大屏监控 (UI.Wpf) 统一访问同一数据库文件
/// </summary>
public static class ScadaDatabaseLocator
{
    private static string? _cachedDbPath;

    public static string GetDatabaseFilePath()
    {
        if (_cachedDbPath != null) return _cachedDbPath;

        // 1. 优先检查环境变量
        var envPath = Environment.GetEnvironmentVariable("SCADA_CONFIG_DB");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            _cachedDbPath = envPath;
            return _cachedDbPath;
        }

        // 2. 向上递归查找工程/解决方案根目录 (包含 .sln 或 src 目录)
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var current = new DirectoryInfo(baseDir);
        while (current != null)
        {
            if (current.GetFiles("*.sln").Length > 0 || current.GetDirectories("src").Length > 0)
            {
                var rootDb = Path.Combine(current.FullName, "scada_config.db");
                _cachedDbPath = rootDb;
                return _cachedDbPath;
            }
            current = current.Parent;
        }

        // 3. 生产发布环境统一目录：%ProgramData%\UniversalScada\scada_config.db
        var programData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UniversalScada");
        Directory.CreateDirectory(programData);
        _cachedDbPath = Path.Combine(programData, "scada_config.db");
        return _cachedDbPath;
    }

    public static string GetConnectionString() => $"Data Source={GetDatabaseFilePath()}";
}

/// <summary>
/// SQLite 存储服务依赖注入扩展方法
/// </summary>
public static class SqliteStorageExtensions
{
    /// <summary>
    /// 注册基于 SQLite 的组态配置仓储服务 (IConfigRepository)
    /// </summary>
    /// <param name="services">DI 服务容器</param>
    /// <param name="connectionString">SQLite 数据库连接字符串 (默认自动使用 ScadaDatabaseLocator 统一定位的 scada_config.db)</param>
    /// <returns>服务容器本身</returns>
    public static IServiceCollection AddSqliteConfigStorage(
        this IServiceCollection services, 
        string? connectionString = null)
    {
        var connStr = string.IsNullOrWhiteSpace(connectionString) 
            ? ScadaDatabaseLocator.GetConnectionString() 
            : connectionString;

        services.AddSingleton<IConfigRepository>(_ => new SqliteConfigRepository(connStr));
        services.AddSingleton<IUserRepository>(_ => new SqliteUserRepository(connStr));
        return services;
    }
}
