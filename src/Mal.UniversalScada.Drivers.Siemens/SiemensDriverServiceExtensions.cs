using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Drivers.Siemens;

/// <summary>
/// 西门子 S7 协议驱动服务注册扩展方法
/// </summary>
public static class SiemensDriverServiceExtensions
{
    /// <summary>
    /// 向依赖注入容器中注册西门子 S7 协议驱动 (SiemensS7Driver) 并登记至 DriverFactory
    /// </summary>
    public static IServiceCollection AddSiemensS7Driver(this IServiceCollection services)
    {
        services.TryAddSingleton<IDriverFactory, DefaultDriverFactory>();
        services.AddTransient<SiemensS7Driver>();

        DefaultDriverFactory.RegisterDriver<SiemensS7Driver>(ProtocolType.SiemensS7, "SiemensS7");

        return services;
    }
}
