using System.Buffers.Binary;
using System.Diagnostics;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Drivers;

/// <summary>
/// 欧姆龙 FINS 工业协议驱动实现 (支持 FINS TCP / UDP 格式)
/// 支持 CS/CJ/CP/NJ/NX 等主流系列 PLC 的 DM, CIO, WR, HR 等区域的高性能批量读写
/// </summary>
public class OmronFinsDriver : IDriver
{
    private readonly SemaphoreSlim _commLock = new(1, 1);
    private IChannel? _channel;
    private DeviceNode? _device;
    private bool _isInitialized;
    private bool _isDisposed;
    private byte _sid = 1;

    /// <inheritdoc />
    public ProtocolType ProtocolType => ProtocolType.OmronFins;

    /// <inheritdoc />
    public string ProtocolName => "OmronFins";

    /// <summary>
    /// 是否使用 FINS TCP 封装模式 (带 FINS TCP 16 字节头部)
    /// </summary>
    public bool IsTcpMode { get; set; } = true;

    /// <summary>
    /// 目标 PLC 节点号 (DA1)
    /// </summary>
    public byte PlcNode { get; set; } = 0x01;

    /// <summary>
    /// 本机 PC 节点号 (SA1)
    /// </summary>
    public byte PcNode { get; set; } = 0x0A;

    /// <summary>
    /// 网络号 (DNA / SNA)
    /// </summary>
    public byte NetworkNo { get; set; } = 0x00;

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

            // 如果是 TCP 模式，执行 FINS TCP 节点握手 (Command 0)
            if (IsTcpMode)
            {
                var handshake = BuildTcpHandshakeRequest(PcNode);
                await channel.SendAsync(handshake, 0, handshake.Length, ct);

                byte[] handshakeResp = new byte[24];
                int totalRead = 0;
                while (totalRead < 24)
                {
                    int r = await channel.ReceiveAsync(handshakeResp, totalRead, 24 - totalRead, ct);
                    if (r <= 0) break;
                    totalRead += r;
                }

                if (totalRead >= 24 && ParseTcpHandshakeResponse(handshakeResp, out var clientNode, out var serverNode))
                {
                    if (clientNode != 0) PcNode = clientNode;
                    if (serverNode != 0) PlcNode = serverNode;
                }
            }

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
                if (!TryParseAddress(tag.Address, out var finsAddr))
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
                    ushort points = GetWordCountForType(tag.DataType, finsAddr.IsBit);
                    byte sid = unchecked(_sid++);
                    var finsFrame = BuildReadCommand(finsAddr, points, NetworkNo, PlcNode, PcNode, sid);
                    var sendFrame = IsTcpMode ? WrapTcpHeader(finsFrame) : finsFrame;

                    _channel.ClearBuffer();
                    await _channel.SendAsync(sendFrame, 0, sendFrame.Length, ct);

                    var respBytes = await ReadFinsResponseAsync(_channel, IsTcpMode, ct);
                    if (ParseFinsResponse(respBytes, IsTcpMode, out var endCode, out var data) && endCode == 0)
                    {
                        var (val, raw) = DecodeValue(data, tag.DataType, finsAddr.IsBit);
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
            return WriteResult.Failed(tag.Id, value, "欧姆龙 FINS 通信链路未连接", sw.ElapsedMilliseconds);
        }

        if (!TryParseAddress(tag.Address, out var finsAddr))
        {
            return WriteResult.Failed(tag.Id, value, $"无效的欧姆龙寄存器地址: {tag.Address}", sw.ElapsedMilliseconds);
        }

        await _commLock.WaitAsync(ct);
        try
        {
            byte[] writeData = EncodeValue(value, tag.DataType, finsAddr.IsBit, out ushort points);
            byte sid = unchecked(_sid++);
            var finsFrame = BuildWriteCommand(finsAddr, points, writeData, NetworkNo, PlcNode, PcNode, sid);
            var sendFrame = IsTcpMode ? WrapTcpHeader(finsFrame) : finsFrame;

            _channel.ClearBuffer();
            await _channel.SendAsync(sendFrame, 0, sendFrame.Length, ct);

            var respBytes = await ReadFinsResponseAsync(_channel, IsTcpMode, ct);
            sw.Stop();

            if (ParseFinsResponse(respBytes, IsTcpMode, out var endCode, out _) && endCode == 0)
            {
                return WriteResult.Success(tag.Id, value, sw.ElapsedMilliseconds);
            }

            return WriteResult.Failed(tag.Id, value, $"欧姆龙 FINS 响应异常 EndCode: 0x{endCode:X4}", sw.ElapsedMilliseconds);
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

    #region FINS 报文编解码与静态帮助函数 (供驱动与单元测试复用)

    public record FinsAddress(string Area, byte Code, ushort WordAddress, byte BitAddress, bool IsBit);

    public static bool TryParseAddress(string rawAddress, out FinsAddress address)
    {
        address = new FinsAddress("D", 0x82, 0, 0, false);
        if (string.IsNullOrWhiteSpace(rawAddress)) return false;

        var addrStr = rawAddress.Trim().ToUpperInvariant();
        byte code;
        bool isBit = false;
        string numPart;

        if (addrStr.StartsWith("DM") || addrStr.StartsWith("D"))
        {
            numPart = addrStr.StartsWith("DM") ? addrStr[2..] : addrStr[1..];
            isBit = numPart.Contains('.');
            code = isBit ? (byte)0x02 : (byte)0x82; // DM 区 (0x82 字, 0x02 位)
        }
        else if (addrStr.StartsWith("CIO") || addrStr.StartsWith("C"))
        {
            numPart = addrStr.StartsWith("CIO") ? addrStr[3..] : addrStr[1..];
            isBit = numPart.Contains('.');
            code = isBit ? (byte)0x30 : (byte)0xB0; // CIO 区 (0xB0 字, 0x30 位)
        }
        else if (addrStr.StartsWith("WR") || addrStr.StartsWith("W"))
        {
            numPart = addrStr.StartsWith("WR") ? addrStr[2..] : addrStr[1..];
            isBit = numPart.Contains('.');
            code = isBit ? (byte)0x31 : (byte)0xB1; // WR 区 (0xB1 字, 0x31 位)
        }
        else if (addrStr.StartsWith("HR") || addrStr.StartsWith("H"))
        {
            numPart = addrStr.StartsWith("HR") ? addrStr[2..] : addrStr[1..];
            isBit = numPart.Contains('.');
            code = isBit ? (byte)0x32 : (byte)0xB2; // HR 区 (0xB2 字, 0x32 位)
        }
        else
        {
            return false;
        }

        if (isBit)
        {
            var parts = numPart.Split('.');
            if (parts.Length == 2 && ushort.TryParse(parts[0], out var wAddr) && byte.TryParse(parts[1], out var bAddr))
            {
                address = new FinsAddress(addrStr[..1], code, wAddr, bAddr, true);
                return true;
            }
            return false;
        }
        else
        {
            if (ushort.TryParse(numPart, out var wAddr))
            {
                address = new FinsAddress(addrStr[..1], code, wAddr, 0, false);
                return true;
            }
            return false;
        }
    }

    public static byte[] BuildTcpHandshakeRequest(byte pcNode = 0)
    {
        byte[] frame = new byte[20];
        frame[0] = 0x46; frame[1] = 0x49; frame[2] = 0x4E; frame[3] = 0x53; // "FINS"
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(4, 4), 12);
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(8, 4), 0);
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(12, 4), 0);
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(16, 4), pcNode);
        return frame;
    }

    public static bool ParseTcpHandshakeResponse(byte[] response, out byte clientNode, out byte serverNode)
    {
        clientNode = 0;
        serverNode = 0;
        if (response.Length < 24) return false;
        if (response[0] != 0x46 || response[1] != 0x49 || response[2] != 0x4E || response[3] != 0x53) return false;

        int cmd = BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(8, 4));
        int err = BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(12, 4));
        if (cmd != 1 || err != 0) return false;

        clientNode = (byte)BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(16, 4));
        serverNode = (byte)BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(20, 4));
        return true;
    }

    public static byte[] WrapTcpHeader(byte[] finsFrame)
    {
        byte[] tcpFrame = new byte[16 + finsFrame.Length];
        tcpFrame[0] = 0x46; tcpFrame[1] = 0x49; tcpFrame[2] = 0x4E; tcpFrame[3] = 0x53; // "FINS"
        BinaryPrimitives.WriteInt32BigEndian(tcpFrame.AsSpan(4, 4), finsFrame.Length + 8);
        BinaryPrimitives.WriteInt32BigEndian(tcpFrame.AsSpan(8, 4), 2);
        BinaryPrimitives.WriteInt32BigEndian(tcpFrame.AsSpan(12, 4), 0);
        Array.Copy(finsFrame, 0, tcpFrame, 16, finsFrame.Length);
        return tcpFrame;
    }

    public static byte[] BuildReadCommand(FinsAddress addr, ushort points, byte netNo = 0, byte plcNode = 1, byte pcNode = 10, byte sid = 1)
    {
        byte[] frame = new byte[18];
        frame[0] = 0x80;
        frame[1] = 0x00;
        frame[2] = 0x02;
        frame[3] = netNo;
        frame[4] = plcNode;
        frame[5] = 0x00;
        frame[6] = netNo;
        frame[7] = pcNode;
        frame[8] = 0x00;
        frame[9] = sid;

        frame[10] = 0x01;
        frame[11] = 0x01;

        frame[12] = addr.Code;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(13, 2), addr.WordAddress);
        frame[15] = addr.BitAddress;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(16, 2), points);

        return frame;
    }

    public static byte[] BuildWriteCommand(FinsAddress addr, ushort points, byte[] writeData, byte netNo = 0, byte plcNode = 1, byte pcNode = 10, byte sid = 1)
    {
        byte[] frame = new byte[18 + writeData.Length];
        frame[0] = 0x80;
        frame[1] = 0x00;
        frame[2] = 0x02;
        frame[3] = netNo;
        frame[4] = plcNode;
        frame[5] = 0x00;
        frame[6] = netNo;
        frame[7] = pcNode;
        frame[8] = 0x00;
        frame[9] = sid;

        frame[10] = 0x01;
        frame[11] = 0x02;

        frame[12] = addr.Code;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(13, 2), addr.WordAddress);
        frame[15] = addr.BitAddress;

        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(16, 2), points);
        Array.Copy(writeData, 0, frame, 18, writeData.Length);

        return frame;
    }

    public static bool ParseFinsResponse(byte[] response, bool isTcp, out ushort endCode, out byte[] data)
    {
        endCode = 0xFFFF;
        data = Array.Empty<byte>();

        int offset = 0;
        if (isTcp)
        {
            if (response.Length < 16) return false;
            if (response[0] != 0x46 || response[1] != 0x49 || response[2] != 0x4E || response[3] != 0x53) return false;
            int tcpErr = BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(12, 4));
            if (tcpErr != 0) return false;
            offset = 16;
        }

        if (response.Length < offset + 14) return false;
        endCode = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset + 12, 2));

        if (endCode == 0)
        {
            int dataLen = response.Length - (offset + 14);
            if (dataLen > 0)
            {
                data = new byte[dataLen];
                Array.Copy(response, offset + 14, data, 0, dataLen);
            }
            return true;
        }

        return false;
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
                TagDataType.Int16 => BinaryPrimitives.ReadInt16BigEndian(data),
                TagDataType.UInt16 => BinaryPrimitives.ReadUInt16BigEndian(data),
                TagDataType.Int32 when data.Length >= 4 => BinaryPrimitives.ReadInt32BigEndian(data),
                TagDataType.UInt32 when data.Length >= 4 => BinaryPrimitives.ReadUInt32BigEndian(data),
                TagDataType.Float when data.Length >= 4 => BinaryPrimitives.ReadSingleBigEndian(data),
                TagDataType.Double when data.Length >= 8 => BinaryPrimitives.ReadDoubleBigEndian(data),
                _ => BinaryPrimitives.ReadInt16BigEndian(data)
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
            return new byte[] { (byte)(b ? 1 : 0) };
        }

        points = GetWordCountForType(type, false);
        byte[] buffer = new byte[points * 2];

        switch (type)
        {
            case TagDataType.Bool:
                BinaryPrimitives.WriteInt16BigEndian(buffer, (short)(Convert.ToBoolean(value) ? 1 : 0));
                break;
            case TagDataType.Int16:
                BinaryPrimitives.WriteInt16BigEndian(buffer, Convert.ToInt16(value));
                break;
            case TagDataType.UInt16:
                BinaryPrimitives.WriteUInt16BigEndian(buffer, Convert.ToUInt16(value));
                break;
            case TagDataType.Int32:
                BinaryPrimitives.WriteInt32BigEndian(buffer, Convert.ToInt32(value));
                break;
            case TagDataType.UInt32:
                BinaryPrimitives.WriteUInt32BigEndian(buffer, Convert.ToUInt32(value));
                break;
            case TagDataType.Float:
                BinaryPrimitives.WriteSingleBigEndian(buffer, Convert.ToSingle(value));
                break;
            case TagDataType.Double:
                BinaryPrimitives.WriteDoubleBigEndian(buffer, Convert.ToDouble(value));
                break;
            default:
                BinaryPrimitives.WriteInt16BigEndian(buffer, Convert.ToInt16(value));
                break;
        }

        return buffer;
    }

    private static async Task<byte[]> ReadFinsResponseAsync(IChannel channel, bool isTcp, CancellationToken ct)
    {
        if (isTcp)
        {
            byte[] tcpHeader = new byte[16];
            int readH = 0;
            while (readH < 16)
            {
                int r = await channel.ReceiveAsync(tcpHeader, readH, 16 - readH, ct);
                if (r <= 0) throw new InvalidOperationException("通信连接已断开");
                readH += r;
            }

            int totalLen = BinaryPrimitives.ReadInt32BigEndian(tcpHeader.AsSpan(4, 4));
            int remaining = totalLen - 8;
            byte[] full = new byte[16 + remaining];
            Array.Copy(tcpHeader, 0, full, 0, 16);

            int readR = 0;
            while (readR < remaining)
            {
                int r = await channel.ReceiveAsync(full, 16 + readR, remaining - readR, ct);
                if (r <= 0) break;
                readR += r;
            }

            return full;
        }
        else
        {
            byte[] buffer = new byte[2048];
            int n = await channel.ReceiveAsync(buffer, 0, buffer.Length, ct);
            byte[] resp = new byte[n];
            Array.Copy(buffer, 0, resp, 0, n);
            return resp;
        }
    }

    #endregion
}
