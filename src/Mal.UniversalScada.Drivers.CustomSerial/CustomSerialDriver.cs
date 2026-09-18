using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.CustomSerial.Enums;
using Mal.UniversalScada.Drivers.CustomSerial.Models;
using Mal.UniversalScada.Drivers.CustomSerial.Protocol;

namespace Mal.UniversalScada.Drivers.CustomSerial;

/// <summary>
/// 串口私有协议 (单片机/嵌入式设备/传感器/仪表) 工业驱动实现
/// 严格实现 IDriver 统一工业协议抽象接口
/// 支持二进制帧头帧尾、校验和 (Sum8/XOR/CRC)、ASCII 文本行、多命令轮询与写控制
/// </summary>
public class CustomSerialDriver : IDriver
{
    private readonly SemaphoreSlim _commLock = new(1, 1);
    private IChannel? _channel;
    private DeviceNode? _device;
    private CustomSerialConfig _config = new();
    private bool _isInitialized;
    private bool _isDisposed;

    /// <inheritdoc />
    public ProtocolType ProtocolType => ProtocolType.CustomSerial;

    /// <inheritdoc />
    public string ProtocolName => "CustomSerial";

    /// <summary>
    /// 当前驱动是否处于就绪初始化状态
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 当前驱动生效的配置参数
    /// </summary>
    public CustomSerialConfig Config => _config;

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
            _config = CustomSerialConfig.FromDeviceNode(device);

            // 1. 确保底层物理通道处于打开连接状态
            if (!channel.IsOpen)
            {
                var opened = await channel.OpenAsync(ct);
                if (!opened)
                {
                    _isInitialized = false;
                    return false;
                }
            }

            channel.ClearBuffer();
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

        if (!_isInitialized || _channel == null || !_channel.IsOpen || _device == null)
        {
            var now = DateTime.Now;
            foreach (var t in tagList)
            {
                results[t.TagId] = new TagValueSnapshot
                {
                    TagId = t.TagId,
                    Quality = QualityCode.CommFailure,
                    Timestamp = now
                };
            }
            return results;
        }

        // 1. 解析点位地址
        var parsedList = new List<(TagNode Tag, CustomSerialAddress Address)>(tagList.Count);
        foreach (var t in tagList)
        {
            try
            {
                var addr = CustomSerialAddressParser.Parse(t.Address, t.DataType);
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

        if (parsedList.Count == 0) return results;

        await _commLock.WaitAsync(ct);
        try
        {
            // 2. 按指令码 (Command) 分组执行多路查询
            var cmdGroups = parsedList.GroupBy(x => x.Address.Command);

            foreach (var group in cmdGroups)
            {
                byte cmd = group.Key;
                try
                {
                    if (_config.FrameMode == CustomSerialFrameMode.AsciiLine)
                    {
                        // ASCII 模式: 发送换行查询指令并接收单行文本
                        var sendStr = $"CMD{cmd:X2}\r\n";
                        var sendBytes = Encoding.ASCII.GetBytes(sendStr);
                        await _channel.SendAsync(sendBytes, 0, sendBytes.Length, ct);

                        var line = await ReceiveAsciiLineAsync(_channel, _config.TimeoutMs, ct);
                        var sampleTime = DateTime.Now;

                        foreach (var (tag, addr) in group)
                        {
                            var (val, raw) = CustomSerialCodec.DecodeAsciiLine(line, addr, tag);
                            results[tag.TagId] = new TagValueSnapshot
                            {
                                TagId = tag.TagId,
                                Value = val,
                                RawValue = raw,
                                Quality = val != null ? QualityCode.Good : QualityCode.Bad,
                                Timestamp = sampleTime
                            };
                        }
                    }
                    else
                    {
                        // 二进制模式: 发送标准帧
                        var reqFrame = CustomSerialCodec.BuildBinaryRequest(_config, cmd, []);
                        await _channel.SendAsync(reqFrame, 0, reqFrame.Length, ct);

                        var respFrame = await ReceiveBinaryFrameAsync(_channel, _config, ct);
                        if (!CustomSerialCodec.TryUnpackBinaryResponse(respFrame, _config, out var respCmd, out var payload, out var err))
                        {
                            throw new InvalidOperationException($"应答帧校验失败: {err}");
                        }

                        var sampleTime = DateTime.Now;
                        foreach (var (tag, addr) in group)
                        {
                            var (val, raw) = CustomSerialCodec.DecodePayload(payload, addr, tag, _config.IsBigEndian);
                            results[tag.TagId] = new TagValueSnapshot
                            {
                                TagId = tag.TagId,
                                Value = val,
                                RawValue = raw,
                                Quality = val != null ? QualityCode.Good : QualityCode.Bad,
                                Timestamp = sampleTime
                            };
                        }
                    }
                }
                catch
                {
                    var failTime = DateTime.Now;
                    foreach (var (tag, _) in group)
                    {
                        results[tag.TagId] = new TagValueSnapshot
                        {
                            TagId = tag.TagId,
                            Quality = QualityCode.CommFailure,
                            Timestamp = failTime
                        };
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
        ArgumentNullException.ThrowIfNull(value);

        if (tag.AccessMode == TagAccessMode.ReadOnly)
        {
            return WriteResult.Failed(tag.TagId, value, "该点位配置为只读 (ReadOnly)，禁止写入");
        }

        if (!_isInitialized || _channel == null || !_channel.IsOpen || _device == null)
        {
            return WriteResult.Failed(tag.TagId, value, "串口驱动未初始化或通道未连接就绪");
        }

        CustomSerialAddress addr;
        try
        {
            addr = CustomSerialAddressParser.Parse(tag.Address, tag.DataType);
        }
        catch (Exception ex)
        {
            return WriteResult.Failed(tag.TagId, value, $"点位地址解析错误: {ex.Message}");
        }

        var sw = Stopwatch.StartNew();

        await _commLock.WaitAsync(ct);
        try
        {
            if (_config.FrameMode == CustomSerialFrameMode.AsciiLine)
            {
                // ASCII 指令写入: "SET,val\r\n"
                var cmdStr = $"SET={value}\r\n";
                var sendBytes = Encoding.ASCII.GetBytes(cmdStr);
                await _channel.SendAsync(sendBytes, 0, sendBytes.Length, ct);

                var respLine = await ReceiveAsciiLineAsync(_channel, _config.TimeoutMs, ct);
                sw.Stop();
                return WriteResult.Success(tag.TagId, value, sw.ElapsedMilliseconds);
            }

            // 二进制写入模式:
            // 构建写入载荷: [Offset(1), Data(N)]
            var valBytes = EncodeValue(value, tag.DataType, _config.IsBigEndian);
            var payload = new byte[1 + valBytes.Length];
            payload[0] = (byte)addr.ByteOffset;
            valBytes.CopyTo(payload, 1);

            // 写指令通常为 0x05 或 Command + 0x10
            byte writeCmd = (byte)(addr.Command >= 0x80 ? addr.Command : (addr.Command + 0x04));
            var sendFrame = CustomSerialCodec.BuildBinaryRequest(_config, writeCmd, payload);

            await _channel.SendAsync(sendFrame, 0, sendFrame.Length, ct);

            var respFrame = await ReceiveBinaryFrameAsync(_channel, _config, ct);
            if (!CustomSerialCodec.TryUnpackBinaryResponse(respFrame, _config, out var respCmd, out var respPayload, out var err))
            {
                sw.Stop();
                return WriteResult.Failed(tag.TagId, value, $"写入应答校验失败: {err}", sw.ElapsedMilliseconds);
            }

            sw.Stop();
            return WriteResult.Success(tag.TagId, value, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return WriteResult.Failed(tag.TagId, value, $"串口写入通信失败: {ex.Message}", sw.ElapsedMilliseconds);
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
        var results = new Dictionary<string, WriteResult>();
        foreach (var kv in writes)
        {
            results[kv.Key.TagId] = await WriteTagAsync(kv.Key, kv.Value, ct);
        }
        return results;
    }

    #region 报文帧接收核心逻辑

    private static async Task<byte[]> ReceiveBinaryFrameAsync(
        IChannel channel, 
        CustomSerialConfig config, 
        CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(config.TimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var token = linkedCts.Token;

        var buffer = new List<byte>(128);
        var oneByte = new byte[1];

        while (!token.IsCancellationRequested)
        {
            var read = await channel.ReceiveAsync(oneByte, 0, 1, token);
            if (read <= 0)
            {
                throw new IOException("串口接收数据流中断");
            }

            buffer.Add(oneByte[0]);

            // 检查是否满足帧尾结束符
            if (config.Tail.Length > 0 && buffer.Count >= config.Tail.Length)
            {
                bool tailMatched = true;
                for (int i = 0; i < config.Tail.Length; i++)
                {
                    if (buffer[buffer.Count - config.Tail.Length + i] != config.Tail[i])
                    {
                        tailMatched = false;
                        break;
                    }
                }

                if (tailMatched)
                {
                    return buffer.ToArray();
                }
            }
            else if (config.Tail.Length == 0)
            {
                // 若未配置帧尾，当接收到 [Header, Station, Cmd, Length] 时，按 Length 决定剩余读取字节数
                var headerLen = config.Header.Length;
                if (buffer.Count >= headerLen + 3)
                {
                    var dataLen = buffer[headerLen + 2];
                    var checkLen = config.CheckType switch
                    {
                        CustomSerialCheckType.Sum8 or CustomSerialCheckType.Xor8 => 1,
                        CustomSerialCheckType.Crc16Modbus or CustomSerialCheckType.Crc16Ccitt => 2,
                        _ => 0
                    };

                    var targetTotal = headerLen + 3 + dataLen + checkLen;
                    if (buffer.Count >= targetTotal)
                    {
                        return buffer.ToArray();
                    }
                }
            }

            if (buffer.Count > 1024)
            {
                throw new InvalidOperationException("串口接收报文超出单帧最大长度保护限制 (1024 字节)");
            }
        }

        throw new TimeoutException("串口接收报文超时");
    }

    private static async Task<string> ReceiveAsciiLineAsync(
        IChannel channel, 
        int timeoutMs, 
        CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var token = linkedCts.Token;

        var buffer = new List<byte>(128);
        var oneByte = new byte[1];

        while (!token.IsCancellationRequested)
        {
            var read = await channel.ReceiveAsync(oneByte, 0, 1, token);
            if (read <= 0)
            {
                throw new IOException("串口 ASCII 接收中断");
            }

            buffer.Add(oneByte[0]);
            if (oneByte[0] == (byte)'\n')
            {
                break;
            }

            if (buffer.Count > 1024)
            {
                throw new InvalidOperationException("串口 ASCII 报文超出单行最大长度限制");
            }
        }

        return Encoding.ASCII.GetString(buffer.ToArray());
    }

    private static byte[] EncodeValue(object val, TagDataType dataType, bool isBigEndian)
    {
        switch (dataType)
        {
            case TagDataType.Bool:
                return [Convert.ToBoolean(val) ? (byte)1 : (byte)0];

            case TagDataType.Int8:
                return [(byte)Convert.ToSByte(val)];

            case TagDataType.UInt8:
                return [Convert.ToByte(val)];

            case TagDataType.Int16:
            {
                var buf = new byte[2];
                short s = Convert.ToInt16(val);
                if (isBigEndian) BinaryPrimitives.WriteInt16BigEndian(buf, s);
                else BinaryPrimitives.WriteInt16LittleEndian(buf, s);
                return buf;
            }

            case TagDataType.UInt16:
            {
                var buf = new byte[2];
                ushort us = Convert.ToUInt16(val);
                if (isBigEndian) BinaryPrimitives.WriteUInt16BigEndian(buf, us);
                else BinaryPrimitives.WriteUInt16LittleEndian(buf, us);
                return buf;
            }

            case TagDataType.Int32:
            {
                var buf = new byte[4];
                int i = Convert.ToInt32(val);
                if (isBigEndian) BinaryPrimitives.WriteInt32BigEndian(buf, i);
                else BinaryPrimitives.WriteInt32LittleEndian(buf, i);
                return buf;
            }

            case TagDataType.Float:
            {
                var buf = new byte[4];
                float f = Convert.ToSingle(val);
                if (isBigEndian) BinaryPrimitives.WriteSingleBigEndian(buf, f);
                else BinaryPrimitives.WriteSingleLittleEndian(buf, f);
                return buf;
            }

            case TagDataType.Double:
            {
                var buf = new byte[8];
                double d = Convert.ToDouble(val);
                if (isBigEndian) BinaryPrimitives.WriteDoubleBigEndian(buf, d);
                else BinaryPrimitives.WriteDoubleLittleEndian(buf, d);
                return buf;
            }

            default:
            {
                var buf = new byte[2];
                short s = Convert.ToInt16(val);
                if (isBigEndian) BinaryPrimitives.WriteInt16BigEndian(buf, s);
                else BinaryPrimitives.WriteInt16LittleEndian(buf, s);
                return buf;
            }
        }
    }

    #endregion

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _isInitialized = false;
        _commLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
