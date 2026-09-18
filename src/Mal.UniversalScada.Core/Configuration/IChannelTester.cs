using System.Diagnostics;
using System.IO.Ports;
using System.Net.Sockets;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Configuration;

/// <summary>
/// 通道测试结果模型
/// </summary>
public record ChannelTestResult(bool IsSuccess, string Message, long ElapsedMs = 0);

/// <summary>
/// 通信通道硬件与网络连通性测试服务
/// </summary>
public interface IChannelTester
{
    /// <summary>
    /// 测试通道连通性 (TCP 握手探测或串口打开测试)
    /// </summary>
    Task<ChannelTestResult> TestChannelAsync(ChannelConfig channel, CancellationToken ct = default);

    /// <summary>
    /// 获取本机当前识别到的所有物理/虚拟串口列表 (如 COM1, COM2, COM3)
    /// </summary>
    IReadOnlyList<string> GetAvailableSerialPorts();
}

/// <summary>
/// 通道连通性测试实现类
/// </summary>
public class DefaultChannelTester : IChannelTester
{
    public async Task<ChannelTestResult> TestChannelAsync(ChannelConfig channel, CancellationToken ct = default)
    {
        if (channel == null)
            return new ChannelTestResult(false, "通道配置为空");

        var sw = Stopwatch.StartNew();

        if (channel.ChannelType == ChannelType.TcpClient)
        {
            if (string.IsNullOrWhiteSpace(channel.Host) || channel.Port <= 0)
                return new ChannelTestResult(false, "主机 IP 或端口号无效");

            try
            {
                using var client = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(channel.ReadTimeoutMs > 0 ? channel.ReadTimeoutMs : 2000);

                await client.ConnectAsync(channel.Host, channel.Port, cts.Token);
                sw.Stop();
                return new ChannelTestResult(true, $"TCP 握手成功！耗时: {sw.ElapsedMilliseconds} ms", sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                return new ChannelTestResult(false, $"连接超时 ({channel.ReadTimeoutMs} ms)，目标主机未响应", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new ChannelTestResult(false, $"连接失败: {ex.Message}", sw.ElapsedMilliseconds);
            }
        }
        else if (channel.ChannelType == ChannelType.SerialPort)
        {
            if (string.IsNullOrWhiteSpace(channel.PortName))
                return new ChannelTestResult(false, "未指定串口号 (PortName)");

            try
            {
                using var serial = new SerialPort(channel.PortName, channel.BaudRate, Parity.None, channel.DataBits, StopBits.One);
                serial.Open();
                bool isOpen = serial.IsOpen;
                serial.Close();
                sw.Stop();
                return new ChannelTestResult(isOpen, $"串口 {channel.PortName} 握手探测成功！状态正常", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new ChannelTestResult(false, $"串口打开失败: {ex.Message} (可能已被其它软件占用或硬件未连接)", sw.ElapsedMilliseconds);
            }
        }

        return new ChannelTestResult(false, $"暂不支持对 {channel.ChannelType} 类型的直接探针测试");
    }

    public IReadOnlyList<string> GetAvailableSerialPorts()
    {
        try
        {
            return SerialPort.GetPortNames().OrderBy(x => x).ToList();
        }
        catch
        {
            return new List<string> { "COM1", "COM2", "COM3" };
        }
    }
}
