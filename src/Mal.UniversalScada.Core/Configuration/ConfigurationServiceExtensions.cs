using Microsoft.Extensions.DependencyInjection;
using Mal.UniversalScada.Core.Configuration;

namespace Mal.UniversalScada.Core.Configuration;

/// <summary>
/// 工业 SCADA 核心组态配置服务依赖注入扩展方法
/// </summary>
public static class ConfigurationServiceExtensions
{
    /// <summary>
    /// 注册工业 SCADA 核心组态业务服务引擎 (组态管理引擎、认证服务、导入导出、硬件连通性探测)
    /// </summary>
    public static IServiceCollection AddScadaConfigurationCore(this IServiceCollection services)
    {
        services.AddSingleton<IAdminAuthService, DefaultAdminAuthService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ITagImportExportService, CsvTagImportExportService>();
        services.AddSingleton<IChannelTester, DefaultChannelTester>();
        return services;
    }
}
