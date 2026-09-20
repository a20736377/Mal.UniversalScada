using System.Buffers.Binary;
using System.Diagnostics;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Drivers;

/// <summary>
/// 三菱 MELSEC MC 协议 (Qna 3E 帧二进制通信) 工业驱动实现
/// 支持 Q/L/iQ-R/FX 等主流系列 PLC 的 D, M, X, Y, W, R 等寄存器区域的批量读写
/// </summary>
public class MitsubishiMcDriver : IDriver
{
    private readonly SemaphoreSlim _commLock = new(1, 1);
    private IChannel? _channel;
    private DeviceNode? _device;
    private bool _isInitialized;
    private bool _isDisposed;

    /// <inheritdoc />
    public ProtocolType ProtocolType => ProtocolType.MitsubishiMc;

    /// <inheritdoc />
    public string ProtocolName => "MitsubishiMc";

    /// <summary>
    /// PLC 网络号 (默认 0)
    /// </summary>
    public byte NetworkNo { get; set; } = 0x00;

    /// <summary>
    /// PLC 站号 (默认 0xFF)
    /// </summary>
    public byte PlcNo { get; set; } = 0xFF;

    /// <summary>
    /// 目标模块 I/O 编号 (默认 0x03FF)
    /// </summary>
    public ushort TargetIo { get; set; } = 0x03FF;

    /// <summary>
    /// 目标模块多点站号 (默认 0x00)
    /// </summary>
    public byte TargetStationNo { get; set; } = 0x00;

    /// <summary>
    /// 监控定时器 (单位 250ms，默认 16 = 4s)
    /// </summary>
    public ushort MonitoringTimer { get; set; } = 0x0010;

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
    public async Task<IReadOnlyDictionary<long, TagValueSnapshot>> ReadBatchAsync(IEnumerable<TagNode> tags, CancellationToken ct = default)
    {
        var tagList = tags.ToList();
        var results = new Dictionary<long, TagValueSnapshot>(tagList.Count);
        if (tagList.Count == 0) return results;

        if (!_isInitialized || _channel == null || !_channel.IsOpen)
        {
            foreach (var t in tagList)
            {
                results[t.Id] = new TagValueSnapshot
                {
                    TagId = t.Id,
                    Quality = QualityCode.CommFailure,
                    Timestamp = DateTime.Now
                };
            }
            return results;
        }

        await _commLock.WaitAsync(ct);
        try
        {
            foreach (var tag in tagList)
            {
                if (!TryParseAddress(tag.Address, out var mcAddr))
                {
                    results[tag.Id] = new TagValueSnapshot
                    {
                        TagId = tag.Id,
                        Quality = QualityCode.Bad,
                        Timestamp = DateTime.Now
                    };
                    continue;
                }

                try
                {
                    ushort points = GetWordCountForType(tag.DataType, mcAddr.IsBit);
                    var req = BuildReadCommand(mcAddr, points, NetworkNo, PlcNo, TargetIo, TargetStationNo, MonitoringTimer);

                    _channel.ClearBuffer();
                    await _channel.SendAsync(req, 0, req.Length, ct);

                    var resp = await ReadFullResponseAsync(_channel, ct);
                    if (ParseResponse(resp, out var endCode, out var data) && endCode == 0)
                    {
                        var (val, raw) = DecodeValue(data, tag.DataType, mcAddr.IsBit);
                        results[tag.Id] = new TagValueSnapshot
                        {
                            TagId = tag.Id,
                            Value = val,
                            RawValue = raw,
                            Quality = QualityCode.Good,
                            Timestamp = DateTime.Now
                        };
                    }
                    else
                    {
                        results[tag.Id] = new TagValueSnapshot
                        {
                            TagId = tag.Id,
                            Quality = QualityCode.Bad,
                            Timestamp = DateTime.Now
                        };
                    }
                }
                catch
                {
                    results[tag.Id] = new TagValueSnapshot
                    {
                        TagId = tag.Id,
                        Quality = QualityCode.CommFailure,
                        Timestamp = DateTime.Now
                    };
                }
            }
        }
        finally
        {
            _commLock.Release();
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<WriteResult> WriteTagAsync(TagNode tag, object value, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (!_isInitialized || _channel == null || !_channel.IsOpen)
        {
            return WriteResult.Failed(tag.Id, value, "通信链路未就绪或未连接", sw.ElapsedMilliseconds);
        }

        if (!TryParseAddress(tag.Address, out var mcAddr))
        {
            return WriteResult.Failed(tag.Id, value, $"无效的三菱寄存器地址: {tag.Address}", sw.ElapsedMilliseconds);
        }

        await _commLock.WaitAsync(ct);
        try
        {
            byte[] writeData = EncodeValue(value, tag.DataType, mcAddr.IsBit, out ushort points);
            var req = BuildWriteCommand(mcAddr, points, writeData, NetworkNo, PlcNo, TargetIo, TargetStationNo, MonitoringTimer);

            _channel.ClearBuffer();
            await _channel.SendAsync(req, 0, req.Length, ct);

            var resp = await ReadFullResponseAsync(_channel, ct);
            sw.Stop();

            if (ParseResponse(resp, out var endCode, out _) && endCode == 0)
            {
                return WriteResult.Success(tag.Id, value, sw.ElapsedMilliseconds);
            }

            return WriteResult.Failed(tag.Id, value, $"三菱 MC 响应异常 EndCode: 0x{endCode:X4}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return WriteResult.Failed(tag.Id, value, ex.Message, sw.ElapsedMilliseconds);
        }
        finally
        {
            _commLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, WriteResult>> WriteBatchAsync(
        IEnumerable<KeyValuePair<TagNode, object>> writes, 
        CancellationToken ct = default)
    {
        var dict = new Dictionary<long, WriteResult>();
        foreach (var kvp in writes)
        {
            var res = await WriteTagAsync(kvp.Key, kvp.Value, ct);
            dict[kvp.Key.Id] = res;
        }
        return dict;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _channel?.Dispose();
        _commLock.Dispose();
        GC.SuppressFinalize(this);
    }

    #region MC 3E 协议报文编解码静态函数 (供驱动与单元测试复用)

    public record McAddress(string Area, byte Code, int Address, bool IsBit);

    public static bool TryParseAddress(string rawAddress, out McAddress address)
    {
        address = new McAddress("D", 0xA8, 0, false);
        if (string.IsNullOrWhiteSpace(rawAddress)) return false;

        var addrStr = rawAddress.Trim().ToUpperInvariant();
        byte code;
        bool isBit = false;
        string numPart;

        if (addrStr.StartsWith("D"))
        {
            code = 0xA8; // D 寄存器
            numPart = addrStr[1..];
        }
        else if (addrStr.StartsWith("W"))
        {
            code = 0xB4; // W 寄存器 (16进制地址)
            numPart = addrStr[1..];
            if (int.TryParse(numPart, System.Globalization.NumberStyles.HexNumber, null, out var hexVal))
            {
                address = new McAddress("W", code, hexVal, false);
                return true;
            }
            return false;
        }
        else if (addrStr.StartsWith("R"))
        {
            code = 0xAF; // R 文件寄存器
            numPart = addrStr[1..];
        }
        else if (addrStr.StartsWith("M"))
        {
            code = 0x90; // M 内部继电器 (位)
            isBit = true;
            numPart = addrStr[1..];
        }
        else if (addrStr.StartsWith("X"))
        {
            code = 0x9C; // X 输入 (位，8进制)
            isBit = true;
            numPart = addrStr[1..];
            try
            {
                var octVal = Convert.ToInt32(numPart, 8);
                address = new McAddress("X", code, octVal, true);
                return true;
            }
            catch { return false; }
        }
        else if (addrStr.StartsWith("Y"))
        {
            code = 0x9D; // Y 输出 (位，8进制)
            isBit = true;
            numPart = addrStr[1..];
            try
            {
                var octVal = Convert.ToInt32(numPart, 8);
                address = new McAddress("Y", code, octVal, true);
                return true;
            }
            catch { return false; }
        }
        else
        {
            return false;
        }

        if (int.TryParse(numPart, out var num))
        {
            address = new McAddress(addrStr[..1], code, num, isBit);
            return true;
        }

        return false;
    }

    public static byte[] BuildReadCommand(
        McAddress addr, 
        ushort points, 
        byte netNo = 0, 
        byte plcNo = 0xFF, 
        ushort targetIo = 0x03FF, 
        byte stationNo = 0, 
        ushort timer = 0x0010)
    {
        byte[] frame = new byte[21];
        frame[0] = 0x50;
        frame[1] = 0x00;
        frame[2] = netNo;
        frame[3] = plcNo;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4, 2), targetIo);
        frame[6] = stationNo;

        ushort reqDataLen = 12;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(7, 2), reqDataLen);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(9, 2), timer);

        frame[11] = 0x04;
        frame[12] = 0x01;

        ushort subCmd = addr.IsBit ? (ushort)0x0001 : (ushort)0x0000;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(13, 2), subCmd);

        frame[15] = (byte)(addr.Address & 0xFF);
        frame[16] = (byte)((addr.Address >> 8) & 0xFF);
        frame[17] = (byte)((addr.Address >> 16) & 0xFF);

        frame[18] = addr.Code;

        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(19, 2), points);

        return frame;
    }

    public static byte[] BuildWriteCommand(
        McAddress addr, 
        ushort points, 
        byte[] writeData, 
        byte netNo = 0, 
        byte plcNo = 0xFF, 
        ushort targetIo = 0x03FF, 
        byte stationNo = 0, 
        ushort timer = 0x0010)
    {
        ushort reqDataLen = (ushort)(12 + writeData.Length);
        byte[] frame = new byte[9 + reqDataLen];

        frame[0] = 0x50;
        frame[1] = 0x00;
        frame[2] = netNo;
        frame[3] = plcNo;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4, 2), targetIo);
        frame[6] = stationNo;

        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(7, 2), reqDataLen);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(9, 2), timer);

        frame[11] = 0x14;
        frame[12] = 0x01;

        ushort subCmd = addr.IsBit ? (ushort)0x0001 : (ushort)0x0000;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(13, 2), subCmd);

        frame[15] = (byte)(addr.Address & 0xFF);
        frame[16] = (byte)((addr.Address >> 8) & 0xFF);
        frame[17] = (byte)((addr.Address >> 16) & 0xFF);
        frame[18] = addr.Code;

        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(19, 2), points);
        Array.Copy(writeData, 0, frame, 21, writeData.Length);

        return frame;
    }

    public static bool ParseResponse(byte[] response, out ushort endCode, out byte[] data)
    {
        endCode = 0xFFFF;
        data = Array.Empty<byte>();

        if (response.Length < 11) return false;
        if (response[0] != 0xD0 || response[1] != 0x00) return false;

        ushort dataLen = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(7, 2));
        endCode = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(9, 2));

        if (endCode == 0 && response.Length >= 11)
        {
            int payloadLen = response.Length - 11;
            data = new byte[payloadLen];
            Array.Copy(response, 11, data, 0, payloadLen);
            return true;
        }

        return endCode == 0;
    }

    private static ushort GetWordCountForType(TagDataType type, bool isBit)
    {
        if (isBit) return 1;
        return type switch
        {
            TagDataType.Int32 or TagDataType.UInt32 or TagDataType.Float => 2,
            TagDataType.Int64 or TagDataType.UInt64 or TagDataType.Double => 4,
            _ => 1
        };
    }

    private static (object? Value, byte[] Raw) DecodeValue(byte[] data, TagDataType type, bool isBit)
    {
        if (data.Length == 0) return (null, data);

        if (isBit)
        {
            bool bitVal = data[0] != 0;
            return (bitVal, data);
        }

        try
        {
            object? val = type switch
            {
                TagDataType.Bool => (data[0] != 0 || data.Length > 1 && data[1] != 0),
                TagDataType.Int16 => BinaryPrimitives.ReadInt16LittleEndian(data),
                TagDataType.UInt16 => BinaryPrimitives.ReadUInt16LittleEndian(data),
                TagDataType.Int32 when data.Length >= 4 => BinaryPrimitives.ReadInt32LittleEndian(data),
                TagDataType.UInt32 when data.Length >= 4 => BinaryPrimitives.ReadUInt32LittleEndian(data),
                TagDataType.Float when data.Length >= 4 => BinaryPrimitives.ReadSingleLittleEndian(data),
                TagDataType.Double when data.Length >= 8 => BinaryPrimitives.ReadDoubleLittleEndian(data),
                _ => BinaryPrimitives.ReadInt16LittleEndian(data)
            };
            return (val, data);
        }
        catch
        {
            return (null, data);
        }
    }

    private static byte[] EncodeValue(object value, TagDataType type, bool isBit, out ushort points)
    {
        if (isBit)
        {
            points = 1;
            bool b = Convert.ToBoolean(value);
            return new byte[] { (byte)(b ? 0x10 : 0x00) };
        }

        points = GetWordCountForType(type, false);
        byte[] buffer = new byte[points * 2];

        switch (type)
        {
            case TagDataType.Bool:
                BinaryPrimitives.WriteInt16LittleEndian(buffer, (short)(Convert.ToBoolean(value) ? 1 : 0));
                break;
            case TagDataType.Int16:
                BinaryPrimitives.WriteInt16LittleEndian(buffer, Convert.ToInt16(value));
                break;
            case TagDataType.UInt16:
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, Convert.ToUInt16(value));
                break;
            case TagDataType.Int32:
                BinaryPrimitives.WriteInt32LittleEndian(buffer, Convert.ToInt32(value));
                break;
            case TagDataType.UInt32:
                BinaryPrimitives.WriteUInt32LittleEndian(buffer, Convert.ToUInt32(value));
                break;
            case TagDataType.Float:
                BinaryPrimitives.WriteSingleLittleEndian(buffer, Convert.ToSingle(value));
                break;
            case TagDataType.Double:
                BinaryPrimitives.WriteDoubleLittleEndian(buffer, Convert.ToDouble(value));
                break;
            default:
                BinaryPrimitives.WriteInt16LittleEndian(buffer, Convert.ToInt16(value));
                break;
        }

        return buffer;
    }

    private static async Task<byte[]> ReadFullResponseAsync(IChannel channel, CancellationToken ct)
    {
        byte[] header = new byte[11];
        int readHeader = 0;
        while (readHeader < 11)
        {
            int n = await channel.ReceiveAsync(header, readHeader, 11 - readHeader, ct);
            if (n <= 0) throw new InvalidOperationException("通信连接已断开");
            readHeader += n;
        }

        ushort restLen = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(7, 2));
        int dataToRead = restLen > 2 ? restLen - 2 : 0;
        byte[] fullFrame = new byte[11 + dataToRead];
        Array.Copy(header, 0, fullFrame, 0, 11);

        int readData = 0;
        while (readData < dataToRead)
        {
            int n = await channel.ReceiveAsync(fullFrame, 11 + readData, dataToRead - readData, ct);
            if (n <= 0) break;
            readData += n;
        }

        return fullFrame;
    }

    #endregion
}
