using System.Diagnostics;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.Siemens.Enums;
using Mal.UniversalScada.Drivers.Siemens.Models;
using Mal.UniversalScada.Drivers.Siemens.Protocol;

namespace Mal.UniversalScada.Drivers.Siemens;

/// <summary>
/// 西门子 S7 工业协议驱动实现 (支持 S7-200/Smart/300/400/1200/1500)
/// 严格实现 IDriver 统一工业协议抽象接口
/// </summary>
public class SiemensS7Driver : IDriver
{
    private readonly SemaphoreSlim _commLock = new(1, 1);
    private IChannel? _channel;
    private DeviceNode? _device;
    private S7DeviceConfig _config = new();
    private ushort _pduCounter = 1;
    private bool _isInitialized;
    private bool _isDisposed;

    /// <inheritdoc />
    public ProtocolType ProtocolType => ProtocolType.SiemensS7;

    /// <inheritdoc />
    public string ProtocolName => "SiemensS7";

    /// <summary>
    /// 当前驱动协商成功的 PDU 长度
    /// </summary>
    public ushort NegotiatedPduLength => _config.NegotiatedPduLength;

    /// <summary>
    /// 当前驱动是否处于就绪初始化状态
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <inheritdoc />
    public async Task<bool> InitializeAsync(IChannel channel, DeviceNode device, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(device);

        await _commLock.WaitAsync(ct);
        try
        {
            _channel = channel;
            _device = device;
            _config = S7DeviceConfig.FromDeviceNode(device);

            // 1. 确保物理/网络链路打开
            if (!channel.IsOpen)
            {
                var opened = await channel.OpenAsync(ct);
                if (!opened) return false;
            }

            channel.ClearBuffer();

            // 2. 步骤一: COTP Connection Request 握手 (CR / CC)
            var crPacket = CotpHandler.BuildConnectionRequest(_config.GetLocalTsap(), _config.GetRemoteTsap());
            await channel.SendAsync(crPacket, 0, crPacket.Length, ct);

            using var timeoutCts = new CancellationTokenSource(device.TimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var ccPacket = await CotpHandler.ReceiveFullFrameAsync(channel, linkedCts.Token);
            if (!CotpHandler.ValidateConnectionConfirm(ccPacket, ccPacket.Length))
            {
                _isInitialized = false;
                return false;
            }

            // 3. 步骤二: S7 Setup Communication 协商 PDU 长度与并发作业
            var setupPdu = S7MessageCodec.BuildSetupCommunicationPdu(GetNextPduRef(), _config.PreferredPduLength);
            var setupFrame = CotpHandler.WrapDataPdu(setupPdu);
            await channel.SendAsync(setupFrame, 0, setupFrame.Length, ct);

            var setupRespFrame = await CotpHandler.ReceiveFullFrameAsync(channel, linkedCts.Token);
            var setupRespPdu = CotpHandler.UnwrapDataPdu(setupRespFrame, setupRespFrame.Length);

            _config.NegotiatedPduLength = S7MessageCodec.ParseSetupCommunicationAck(setupRespPdu.Span);
            _isInitialized = true;
            return true;
        }
        catch (Exception)
        {
            _isInitialized = false;
            return false;
        }
        finally
        {
            _commLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        await _commLock.WaitAsync();
        try
        {
            _isInitialized = false;
            if (_channel is { IsOpen: true })
            {
                await _channel.CloseAsync();
            }
        }
        finally
        {
            _commLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, TagValueSnapshot>> ReadBatchAsync(
        IEnumerable<TagNode> tags, 
        CancellationToken ct = default)
    {
        var tagList = tags.ToList();
        var results = new Dictionary<string, TagValueSnapshot>(tagList.Count);
        if (tagList.Count == 0) return results;

        if (!_isInitialized || _channel == null || !_channel.IsOpen)
        {
            foreach (var t in tagList)
            {
                results[t.TagId] = new TagValueSnapshot
                {
                    TagId = t.TagId,
                    Quality = QualityCode.CommFailure,
                    Timestamp = DateTime.Now
                };
            }
            return results;
        }

        // 1. 解析点位地址
        var parsedList = new List<(TagNode Tag, S7Address? Address)>(tagList.Count);
        foreach (var t in tagList)
        {
            try
            {
                var addr = S7AddressParser.Parse(t.Address, t.DataType);
                parsedList.Add((t, addr));
            }
            catch
            {
                results[t.TagId] = new TagValueSnapshot
                {
                    TagId = t.TagId,
                    Quality = QualityCode.Bad,
                    Timestamp = DateTime.Now
                };
            }
        }

        var validItems = parsedList.Where(x => x.Address != null).ToList();
        if (validItems.Count == 0) return results;

        await _commLock.WaitAsync(ct);
        try
        {
            // 2. 按西门子 S7 协议最大项限制 (MaxItemsPerReadJob) 进行分片批量读取
            var chunkSize = Math.Max(1, _config.MaxItemsPerReadJob);
            for (int i = 0; i < validItems.Count; i += chunkSize)
            {
                var chunk = validItems.Skip(i).Take(chunkSize).ToList();
                var addresses = chunk.Select(c => c.Address!).ToList();

                try
                {
                    var pduRef = GetNextPduRef();
                    var readPdu = S7MessageCodec.BuildReadVarPdu(pduRef, addresses);
                    var sendPacket = CotpHandler.WrapDataPdu(readPdu);

                    await _channel.SendAsync(sendPacket, 0, sendPacket.Length, ct);

                    var respPacket = await CotpHandler.ReceiveFullFrameAsync(_channel, ct);
                    var respPdu = CotpHandler.UnwrapDataPdu(respPacket, respPacket.Length);

                    var readResults = S7MessageCodec.ParseReadVarAck(respPdu.Span, chunk.Count);

                    for (int j = 0; j < chunk.Count; j++)
                    {
                        var (tag, addr) = chunk[j];
                        var itemRes = j < readResults.Count ? readResults[j] : null;

                        if (itemRes != null && itemRes.IsSuccess && itemRes.Data.Length > 0)
                        {
                            var (val, raw) = S7DataConverter.DecodeValue(
                                itemRes.Data, 
                                0, 
                                tag, 
                                itemRes.IsBit, 
                                addr!.BitIndex);

                            results[tag.TagId] = new TagValueSnapshot
                            {
                                TagId = tag.TagId,
                                Value = val,
                                RawValue = raw,
                                Quality = QualityCode.Good,
                                Timestamp = DateTime.Now
                            };
                        }
                        else
                        {
                            results[tag.TagId] = new TagValueSnapshot
                            {
                                TagId = tag.TagId,
                                Quality = QualityCode.Bad,
                                Timestamp = DateTime.Now
                            };
                        }
                    }
                }
                catch (Exception)
                {
                    // 本批次通信故障
                    foreach (var (tag, _) in chunk)
                    {
                        if (!results.ContainsKey(tag.TagId))
                        {
                            results[tag.TagId] = new TagValueSnapshot
                            {
                                TagId = tag.TagId,
                                Quality = QualityCode.CommFailure,
                                Timestamp = DateTime.Now
                            };
                        }
                    }
                }
            }

            return results;
        }
        finally
        {
            _commLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<WriteResult> WriteTagAsync(TagNode tag, object value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tag);

        var sw = Stopwatch.StartNew();

        if (!_isInitialized || _channel == null || !_channel.IsOpen)
        {
            return WriteResult.Failed(tag.TagId, value, "S7 驱动未初始化或链路未就绪", 0);
        }

        S7Address address;
        byte[] payload;
        try
        {
            address = S7AddressParser.Parse(tag.Address, tag.DataType);
            payload = S7DataConverter.EncodeValue(value, tag);
        }
        catch (Exception ex)
        {
            return WriteResult.Failed(tag.TagId, value, $"参数解析或转换失败: {ex.Message}", 0);
        }

        await _commLock.WaitAsync(ct);
        try
        {
            var pduRef = GetNextPduRef();
            var writePdu = S7MessageCodec.BuildWriteVarPdu(pduRef, [(address, payload)]);
            var sendPacket = CotpHandler.WrapDataPdu(writePdu);

            await _channel.SendAsync(sendPacket, 0, sendPacket.Length, ct);

            var respPacket = await CotpHandler.ReceiveFullFrameAsync(_channel, ct);
            var respPdu = CotpHandler.UnwrapDataPdu(respPacket, respPacket.Length);

            var writeAcks = S7MessageCodec.ParseWriteVarAck(respPdu.Span, 1);
            sw.Stop();

            if (writeAcks.Count > 0 && writeAcks[0] == S7ReturnCode.Success)
            {
                return WriteResult.Success(tag.TagId, value, sw.ElapsedMilliseconds);
            }

            var err = writeAcks.Count > 0 ? writeAcks[0].ToString() : "未知错误";
            return WriteResult.Failed(tag.TagId, value, $"西门子 PLC 应答写入错误: {err}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return WriteResult.Failed(tag.TagId, value, $"写入异常: {ex.Message}", sw.ElapsedMilliseconds);
        }
        finally
        {
            _commLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, WriteResult>> WriteBatchAsync(
        IEnumerable<KeyValuePair<TagNode, object>> writes, 
        CancellationToken ct = default)
    {
        var list = writes.ToList();
        var results = new Dictionary<string, WriteResult>(list.Count);
        if (list.Count == 0) return results;

        // 顺序或分块写入
        foreach (var kv in list)
        {
            var res = await WriteTagAsync(kv.Key, kv.Value, ct);
            results[kv.Key.TagId] = res;
        }

        return results;
    }

    private ushort GetNextPduRef()
    {
        unchecked
        {
            return _pduCounter++;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _isInitialized = false;
        _commLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
