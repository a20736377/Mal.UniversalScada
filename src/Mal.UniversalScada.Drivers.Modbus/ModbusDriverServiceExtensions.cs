using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Drivers.Modbus;

/// <summary>
/// Modbus 协议驱动服务注册扩展方法
/// </summary>
public static class ModbusDriverServiceExtensions
{
    /// <summary>
    /// 向依赖注入容器中注册 Modbus 系列驱动 (ModbusTcp, ModbusRtu, ModbusAscii) 并登记至 DriverFactory
    /// </summary>
    public static IServiceCollection AddModbusDriver(this IServiceCollection services)
    {
        services.TryAddSingleton<IDriverFactory, DefaultDriverFactory>();

        services.AddTransient<ModbusDriver>();
        services.AddTransient<ModbusTcpDriver>();
        services.AddTransient<ModbusRtuDriver>();
        services.AddTransient<ModbusAsciiDriver>();

        // 登记到静态工厂映射
        DefaultDriverFactory.RegisterDriver<ModbusTcpDriver>(ProtocolType.ModbusTcp, "ModbusTcp");
        DefaultDriverFactory.RegisterDriver<ModbusRtuDriver>(ProtocolType.ModbusRtu, "ModbusRtu");
        DefaultDriverFactory.RegisterDriver<ModbusAsciiDriver>(ProtocolType.ModbusAscii, "ModbusAscii");

        return services;
    }
}
