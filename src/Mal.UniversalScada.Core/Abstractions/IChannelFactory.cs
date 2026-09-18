using Mal.UniversalScada.Core.Channels;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 通信通道工厂接口
/// </summary>
public interface IChannelFactory
{
    /// <summary>
    /// 根据配置创建物理/网络传输通道实例
    /// </summary>
    IChannel CreateChannel(ChannelConfig config);
}

/// <summary>
/// 默认通道工厂实现
/// </summary>
public class DefaultChannelFactory : IChannelFactory
{
    public IChannel CreateChannel(ChannelConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.ChannelType switch
        {
            ChannelType.TcpClient => new TcpClientChannel(config),
            ChannelType.SerialPort => new SerialPortChannel(config),
            _ => throw new NotSupportedException($"当前系统暂不支持传输介质类型: {config.ChannelType}")
        };
    }
}
