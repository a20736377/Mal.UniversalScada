using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Drivers.CustomSerial;

/// <summary>
/// 串口私有协议驱动服务注册扩展
/// </summary>
public static class CustomSerialServiceExtensions
{
    /// <summary>
    /// 向服务容器注册串口私有协议驱动并登记至 DriverFactory
    /// </summary>
    public static IServiceCollection AddCustomSerialDriver(this IServiceCollection services)
    {
        services.TryAddSingleton<IDriverFactory, DefaultDriverFactory>();
        services.AddTransient<CustomSerialDriver>();

        DefaultDriverFactory.RegisterDriver<CustomSerialDriver>(ProtocolType.CustomSerial, "CustomSerial");

        return services;
    }
}
