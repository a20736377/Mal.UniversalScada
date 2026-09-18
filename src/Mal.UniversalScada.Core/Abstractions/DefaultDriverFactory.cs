using Microsoft.Extensions.DependencyInjection;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 默认工业协议驱动工厂实现，负责管理协议类型与驱动具体实现的动态解析与实例化
/// </summary>
public class DefaultDriverFactory : IDriverFactory
{
    private readonly IServiceProvider _serviceProvider;
    private static readonly Dictionary<ProtocolType, Type> _registeredDriverTypes = new();
    private static readonly Dictionary<string, Type> _namedDriverTypes = new(StringComparer.OrdinalIgnoreCase);

    public DefaultDriverFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 注册协议驱动实现类型
    /// </summary>
    public static void RegisterDriver<TDriver>(ProtocolType protocolType, string? protocolName = null) 
        where TDriver : IDriver
    {
        _registeredDriverTypes[protocolType] = typeof(TDriver);
        if (!string.IsNullOrWhiteSpace(protocolName))
        {
            _namedDriverTypes[protocolName] = typeof(TDriver);
        }
    }

    /// <inheritdoc />
    public IDriver CreateDriver(ProtocolType protocolType, string? customProtocolName = null)
    {
        if (protocolType == ProtocolType.Custom && !string.IsNullOrWhiteSpace(customProtocolName))
        {
            return CreateDriver(customProtocolName);
        }

        if (_registeredDriverTypes.TryGetValue(protocolType, out var driverType))
        {
            return InstantiateDriver(driverType);
        }

        var fallbackName = protocolType.ToString();
        if (_namedDriverTypes.TryGetValue(fallbackName, out var namedType))
        {
            return InstantiateDriver(namedType);
        }

        throw new NotSupportedException($"未注册针对协议类型 [{protocolType}] 的驱动实现。");
    }

    /// <inheritdoc />
    public IDriver CreateDriver(string protocolName)
    {
        if (_namedDriverTypes.TryGetValue(protocolName, out var driverType))
        {
            return InstantiateDriver(driverType);
        }

        if (Enum.TryParse<ProtocolType>(protocolName, true, out var pType))
        {
            return CreateDriver(pType);
        }

        throw new NotSupportedException($"未注册针对协议名称 [{protocolName}] 的驱动实现。");
    }

    private IDriver InstantiateDriver(Type driverType)
    {
        if (_serviceProvider != null)
        {
            return (IDriver)ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, driverType);
        }

        return (IDriver)Activator.CreateInstance(driverType)!;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetSupportedProtocols()
    {
        var list = new HashSet<string>(_namedDriverTypes.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var p in _registeredDriverTypes.Keys)
        {
            list.Add(p.ToString());
        }
        return list.ToList();
    }
}
