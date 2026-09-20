using System.Diagnostics;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Configuration;

/// <summary>
/// 点位在线读取测试结果模型
/// </summary>
public record TagTestReadResult(
    bool IsSuccess, 
    object? Value, 
    object? RawValue, 
    QualityCode Quality, 
    string Message, 
    long ElapsedMs = 0);

/// <summary>
/// 点位在线写入测试结果模型
/// </summary>
public record TagTestWriteResult(
    bool IsSuccess, 
    object? TargetValue, 
    string Message, 
    long ElapsedMs = 0);

/// <summary>
/// 点位在线联机读取与写入通信测试接口
/// </summary>
public interface ITagTester
{
    /// <summary>
    /// 测试点位在线实时读取
    /// </summary>
    Task<TagTestReadResult> TestReadTagAsync(
        TagNode tag, 
        DeviceNode device, 
        ChannelConfig channel, 
        CancellationToken ct = default);

    /// <summary>
    /// 测试点位在线控制写入
    /// </summary>
    Task<TagTestWriteResult> TestWriteTagAsync(
        TagNode tag, 
        object writeValue, 
        DeviceNode device, 
        ChannelConfig channel, 
        CancellationToken ct = default);
}

/// <summary>
/// 点位在线测试引擎默认实现类
/// </summary>
public class DefaultTagTester : ITagTester
{
    private readonly IDriverFactory _driverFactory;
    private readonly IChannelFactory _channelFactory;

    public DefaultTagTester(IDriverFactory driverFactory, IChannelFactory channelFactory)
    {
        _driverFactory = driverFactory ?? throw new ArgumentNullException(nameof(driverFactory));
        _channelFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
    }

    public async Task<TagTestReadResult> TestReadTagAsync(
        TagNode tag, 
        DeviceNode device, 
        ChannelConfig channel, 
        CancellationToken ct = default)
    {
        if (tag == null) return new TagTestReadResult(false, null, null, QualityCode.Bad, "目标点位元数据为空");
        if (device == null) return new TagTestReadResult(false, null, null, QualityCode.DeviceOffline, "所属设备节点为空");
        if (channel == null) return new TagTestReadResult(false, null, null, QualityCode.CommFailure, "绑定的通信通道配置为空");

        var sw = Stopwatch.StartNew();
        IChannel? channelInst = null;
        IDriver? driverInst = null;

        try
        {
            channelInst = _channelFactory.CreateChannel(channel);
            driverInst = _driverFactory.CreateDriver(device.ProtocolType, device.CustomProtocolName);

            var timeoutMs = Math.Max(device.TimeoutMs, 2000);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            var initOk = await driverInst.InitializeAsync(channelInst, device, cts.Token);
            if (!initOk)
            {
                sw.Stop();
                return new TagTestReadResult(false, null, null, QualityCode.CommFailure, 
                    $"设备协议初始化握手失败 (驱动: {driverInst.ProtocolName})，请检查 IP/端口/站号及 PLC 运行状态", sw.ElapsedMilliseconds);
            }

            var snapshots = await driverInst.ReadBatchAsync([tag], cts.Token);
            sw.Stop();

            if (snapshots.TryGetValue(tag.Id, out var snapshot))
            {
                if (snapshot.Quality == QualityCode.Good)
                {
                    return new TagTestReadResult(true, snapshot.Value, snapshot.RawValue, snapshot.Quality, 
                        $"读取成功！当前值: {snapshot.Value} (原始采集: {snapshot.RawValue})，往返耗时: {sw.ElapsedMilliseconds} ms", sw.ElapsedMilliseconds);
                }
                else
                {
                    return new TagTestReadResult(false, snapshot.Value, snapshot.RawValue, snapshot.Quality, 
                        $"下位机返回异常品质: {snapshot.Quality}，请检查寄存器地址语法与数据范围", sw.ElapsedMilliseconds);
                }
            }

            return new TagTestReadResult(false, null, null, QualityCode.Bad, 
                "驱动未返回该点位的有效快照数据", sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new TagTestReadResult(false, null, null, QualityCode.CommFailure, 
                $"通信读取超时 ({device.TimeoutMs} ms)，下位机或通道未应答", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TagTestReadResult(false, null, null, QualityCode.Bad, 
                $"读取异常: {ex.Message}", sw.ElapsedMilliseconds);
        }
        finally
        {
            if (driverInst != null)
            {
                try { await driverInst.DisconnectAsync(); } catch { }
                driverInst.Dispose();
            }
            if (channelInst != null)
            {
                try { await channelInst.CloseAsync(); } catch { }
                await channelInst.DisposeAsync();
            }
        }
    }

    public async Task<TagTestWriteResult> TestWriteTagAsync(
        TagNode tag, 
        object writeValue, 
        DeviceNode device, 
        ChannelConfig channel, 
        CancellationToken ct = default)
    {
        if (tag == null) return new TagTestWriteResult(false, writeValue, "目标点位元数据为空");
        if (device == null) return new TagTestWriteResult(false, writeValue, "所属设备节点为空");
        if (channel == null) return new TagTestWriteResult(false, writeValue, "绑定的通信通道配置为空");

        if (tag.AccessMode == TagAccessMode.ReadOnly)
        {
            return new TagTestWriteResult(false, writeValue, "该点位被配置为只读 (ReadOnly)，禁止下发控制写入指令！");
        }

        object targetVal;
        try
        {
            targetVal = ConvertValueToType(writeValue, tag.DataType);
        }
        catch (Exception ex)
        {
            return new TagTestWriteResult(false, writeValue, $"输入值格式无法转换为对应类型 {tag.DataType}: {ex.Message}");
        }

        var sw = Stopwatch.StartNew();
        IChannel? channelInst = null;
        IDriver? driverInst = null;

        try
        {
            channelInst = _channelFactory.CreateChannel(channel);
            driverInst = _driverFactory.CreateDriver(device.ProtocolType, device.CustomProtocolName);

            var timeoutMs = Math.Max(device.TimeoutMs, 2000);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            var initOk = await driverInst.InitializeAsync(channelInst, device, cts.Token);
            if (!initOk)
            {
                sw.Stop();
                return new TagTestWriteResult(false, targetVal, 
                    $"设备握手失败 (驱动: {driverInst.ProtocolName})，下位机未连接", sw.ElapsedMilliseconds);
            }

            var writeRes = await driverInst.WriteTagAsync(tag, targetVal, cts.Token);
            sw.Stop();

            if (writeRes.IsSuccess)
            {
                return new TagTestWriteResult(true, targetVal, 
                    $"写入成功！下发值: {targetVal}，往返耗时: {writeRes.RoundTripTimeMs} ms", writeRes.RoundTripTimeMs);
            }
            else
            {
                return new TagTestWriteResult(false, targetVal, 
                    $"写入失败: {writeRes.ErrorMessage ?? "下位机拒绝或超时"}", writeRes.RoundTripTimeMs);
            }
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new TagTestWriteResult(false, targetVal, 
                $"写入操作通信超时 ({device.TimeoutMs} ms)", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TagTestWriteResult(false, targetVal, 
                $"写入异常: {ex.Message}", sw.ElapsedMilliseconds);
        }
        finally
        {
            if (driverInst != null)
            {
                try { await driverInst.DisconnectAsync(); } catch { }
                driverInst.Dispose();
            }
            if (channelInst != null)
            {
                try { await channelInst.CloseAsync(); } catch { }
                await channelInst.DisposeAsync();
            }
        }
    }

    private static object ConvertValueToType(object raw, TagDataType dataType)
    {
        var str = raw.ToString()?.Trim() ?? string.Empty;

        return dataType switch
        {
            TagDataType.Bool => str switch
            {
                "1" or "true" or "True" or "TRUE" or "ON" or "on" => true,
                "0" or "false" or "False" or "FALSE" or "OFF" or "off" => false,
                _ => bool.Parse(str)
            },
            TagDataType.Int8 => sbyte.Parse(str),
            TagDataType.UInt8 => byte.Parse(str),
            TagDataType.Int16 => short.Parse(str),
            TagDataType.UInt16 => ushort.Parse(str),
            TagDataType.Int32 => int.Parse(str),
            TagDataType.UInt32 => uint.Parse(str),
            TagDataType.Int64 => long.Parse(str),
            TagDataType.UInt64 => ulong.Parse(str),
            TagDataType.Float => float.Parse(str),
            TagDataType.Double => double.Parse(str),
            TagDataType.String => str,
            _ => str
        };
    }
}
