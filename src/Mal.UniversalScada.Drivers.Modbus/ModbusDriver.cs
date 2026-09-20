using System.Buffers.Binary;
using System.Diagnostics;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.Modbus.Enums;
using Mal.UniversalScada.Drivers.Modbus.Models;
using Mal.UniversalScada.Drivers.Modbus.Protocol;
using Mal.UniversalScada.Drivers.Modbus.Protocol.Framers;

namespace Mal.UniversalScada.Drivers.Modbus;

/// <summary>
/// 工业 Modbus 协议驱动主实现 (全面支持 Modbus TCP / RTU / ASCII)
/// 严格实现 IDriver 统一工业协议抽象接口
/// 支持连续地址合并打包 (Packet Packing) 与写优先机制
/// </summary>
public class ModbusDriver : IDriver
{
    protected readonly SemaphoreSlim _commLock = new(1, 1);
    protected IChannel? _channel;
    protected DeviceNode? _device;
    protected ModbusDeviceConfig _config = new();
    protected IModbusFramer? _framer;
    protected bool _isInitialized;
    protected bool _isDisposed;

    public ModbusDriver()
    {
    }

    public ModbusDriver(IModbusFramer framer)
    {
        _framer = framer;
    }

    /// <inheritdoc />
    public virtual ProtocolType ProtocolType => _framer?.ProtocolType ?? ProtocolType.ModbusTcp;

    /// <inheritdoc />
    public virtual string ProtocolName => ProtocolType switch
    {
        ProtocolType.ModbusRtu => "ModbusRtu",
        ProtocolType.ModbusAscii => "ModbusAscii",
        _ => "ModbusTcp"
    };

    /// <summary>
    /// 当前驱动是否处于就绪初始化状态
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 当前驱动生效的从站参数配置
    /// </summary>
    public ModbusDeviceConfig Config => _config;

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
            _config = ModbusDeviceConfig.FromDeviceNode(device);

            // 根据设备配置的协议类型自动选用对应报文封装器 (如果外部未指定)
            _framer ??= SelectFramer(device.ProtocolType);

            // 1. 确保通信通道处于打开状态
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
    public async Task<IReadOnlyDictionary<long, TagValueSnapshot>> ReadBatchAsync(
        IEnumerable<TagNode> tags, 
        CancellationToken ct = default)
    {
        var tagList = tags.ToList();
        var results = new Dictionary<long, TagValueSnapshot>(tagList.Count);
        if (tagList.Count == 0) return results;

        if (!_isInitialized || _channel == null || !_channel.IsOpen || _framer == null || _device == null)
        {
            var now = DateTime.Now;
            foreach (var t in tagList)
            {
                results[t.Id] = new TagValueSnapshot
                {
                    TagId = t.Id,
                    Quality = QualityCode.CommFailure,
                    Timestamp = now
                };
            }
            return results;
        }

        // 1. 解析地址
        var parsedList = new List<(TagNode Tag, ModbusAddress Address)>(tagList.Count);
        foreach (var t in tagList)
        {
            try
            {
                var addr = ModbusAddressParser.Parse(t.Address, t.DataType, _config.ZeroBased);
                parsedList.Add((t, addr));
            }
            catch
            {
                results[t.Id] = new TagValueSnapshot
                {
                    TagId = t.Id,
                    Quality = QualityCode.Bad,
                    Timestamp = DateTime.Now
                };
            }
        }

        if (parsedList.Count == 0) return results;

        await _commLock.WaitAsync(ct);
        try
        {
            // 2. 按寄存器类型 (Coil, DiscreteInput, InputRegister, HoldingRegister) 分组
            var groups = parsedList.GroupBy(x => x.Address.RegisterType);

            foreach (var group in groups)
            {
                var regType = group.Key;
                var isBitArea = regType is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput;
                var maxLimit = isBitArea ? _config.MaxCoilsPerRead : _config.MaxRegistersPerRead;
                var maxGap = isBitArea ? 0 : _config.MaxRegisterGap;

                // 3. 执行连续地址合并打包 (Packet Packing)
                var blocks = PackItemsIntoBlocks(group.ToList(), isBitArea, maxLimit, maxGap);

                foreach (var block in blocks)
                {
                    try
                    {
                        var reqPdu = ModbusPduCodec.BuildReadRequestPdu(regType, block.StartAddress, block.Count);
                        var respPdu = await _framer.SendAndReceivePduAsync(
                            _channel, 
                            _config.SlaveId, 
                            reqPdu, 
                            _device.TimeoutMs, 
                            ct);

                        byte expectedFc = regType switch
                        {
                            ModbusRegisterType.Coil => ModbusPduCodec.FcReadCoils,
                            ModbusRegisterType.DiscreteInput => ModbusPduCodec.FcReadDiscreteInputs,
                            ModbusRegisterType.InputRegister => ModbusPduCodec.FcReadInputRegisters,
                            ModbusRegisterType.HoldingRegister => ModbusPduCodec.FcReadHoldingRegisters,
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        var dataBytes = ModbusPduCodec.ParseReadResponse(respPdu, expectedFc);
                        DistributeReadResults(block, dataBytes, isBitArea, _config.Endian, results, DateTime.Now);
                    }
                    catch (Exception)
                    {
                        var failTime = DateTime.Now;
                        foreach (var (tag, _) in block.Items)
                        {
                            results[tag.Id] = new TagValueSnapshot
                            {
                                TagId = tag.Id,
                                Quality = QualityCode.CommFailure,
                                Timestamp = failTime
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
        ArgumentNullException.ThrowIfNull(value);

        if (tag.AccessMode == TagAccessMode.ReadOnly)
        {
            return WriteResult.Failed(tag.Id, value, "该点位配置为只读 (ReadOnly)，禁止写入");
        }

        if (!_isInitialized || _channel == null || !_channel.IsOpen || _framer == null || _device == null)
        {
            return WriteResult.Failed(tag.Id, value, "Modbus 驱动未初始化或链路通道未连接就绪");
        }

        ModbusAddress addr;
        try
        {
            addr = ModbusAddressParser.Parse(tag.Address, tag.DataType, _config.ZeroBased);
        }
        catch (Exception ex)
        {
            return WriteResult.Failed(tag.Id, value, $"点位地址解析错误: {ex.Message}");
        }

        if (addr.RegisterType is ModbusRegisterType.DiscreteInput or ModbusRegisterType.InputRegister)
        {
            return WriteResult.Failed(tag.Id, value, $"目标寄存器区 [{addr.RegisterType}] 为只读区域，不支持下发写入");
        }

        var sw = Stopwatch.StartNew();

        await _commLock.WaitAsync(ct);
        try
        {
            if (addr.RegisterType == ModbusRegisterType.Coil)
            {
                // 线圈单点写入 (FC 05)
                var boolVal = Convert.ToBoolean(value);
                var reqPdu = ModbusPduCodec.BuildWriteSingleCoilPdu(addr.StartAddress, boolVal);
                var respPdu = await _framer.SendAndReceivePduAsync(
                    _channel, 
                    _config.SlaveId, 
                    reqPdu, 
                    _device.TimeoutMs, 
                    ct);

                ModbusPduCodec.ValidateWriteSingleResponse(respPdu, ModbusPduCodec.FcWriteSingleCoil, addr.StartAddress);
                sw.Stop();
                return WriteResult.Success(tag.Id, value, sw.ElapsedMilliseconds);
            }

            if (addr.RegisterType == ModbusRegisterType.HoldingRegister)
            {
                // 寄存器内位寻址写入 (40001.5 读-改-写机制)
                if (addr.IsBitInRegister)
                {
                    var bitVal = Convert.ToBoolean(value);

                    // 1. 读取当前 1 个寄存器的原始值
                    var readPdu = ModbusPduCodec.BuildReadRequestPdu(ModbusRegisterType.HoldingRegister, addr.StartAddress, 1);
                    var readResp = await _framer.SendAndReceivePduAsync(
                        _channel, 
                        _config.SlaveId, 
                        readPdu, 
                        _device.TimeoutMs, 
                        ct);
                    var readBytes = ModbusPduCodec.ParseReadResponse(readResp, ModbusPduCodec.FcReadHoldingRegisters);

                    // 2. 在同步方法中安全修改位并重新编码 (避免 C# 12 async 方法中的 Span 限制)
                    ushort rawWord = PrepareBitInRegisterWrite(readBytes, addr.BitIndex!.Value, bitVal, _config.Endian);

                    // 3. 写回寄存器 (FC 06)
                    var writePdu = ModbusPduCodec.BuildWriteSingleRegisterPdu(addr.StartAddress, rawWord);
                    var writeResp = await _framer.SendAndReceivePduAsync(
                        _channel, 
                        _config.SlaveId, 
                        writePdu, 
                        _device.TimeoutMs, 
                        ct);

                    ModbusPduCodec.ValidateWriteSingleResponse(writeResp, ModbusPduCodec.FcWriteSingleRegister, addr.StartAddress);
                    sw.Stop();
                    return WriteResult.Success(tag.Id, value, sw.ElapsedMilliseconds);
                }

                // 单寄存器写入 (FC 06)
                if (addr.RegisterCount == 1)
                {
                    var encoded = ModbusDataConverter.EncodeValue(value, tag, _config.Endian);
                    var rawWord = BinaryPrimitives.ReadUInt16BigEndian(encoded);

                    var reqPdu = ModbusPduCodec.BuildWriteSingleRegisterPdu(addr.StartAddress, rawWord);
                    var respPdu = await _framer.SendAndReceivePduAsync(
                        _channel, 
                        _config.SlaveId, 
                        reqPdu, 
                        _device.TimeoutMs, 
                        ct);

                    ModbusPduCodec.ValidateWriteSingleResponse(respPdu, ModbusPduCodec.FcWriteSingleRegister, addr.StartAddress);
                    sw.Stop();
                    return WriteResult.Success(tag.Id, value, sw.ElapsedMilliseconds);
                }
                else
                {
                    // 多寄存器写入 (FC 16 / 0x10) - 32位整型、浮点数、64位双精度等
                    var encoded = ModbusDataConverter.EncodeValue(value, tag, _config.Endian);
                    var reqPdu = ModbusPduCodec.BuildWriteMultipleRegistersPdu(addr.StartAddress, encoded);
                    var respPdu = await _framer.SendAndReceivePduAsync(
                        _channel, 
                        _config.SlaveId, 
                        reqPdu, 
                        _device.TimeoutMs, 
                        ct);

                    ModbusPduCodec.ValidateWriteMultipleResponse(
                        respPdu, 
                        ModbusPduCodec.FcWriteMultipleRegisters, 
                        addr.StartAddress, 
                        addr.RegisterCount);

                    sw.Stop();
                    return WriteResult.Success(tag.Id, value, sw.ElapsedMilliseconds);
                }
            }

            return WriteResult.Failed(tag.Id, value, $"未支持的寄存器写入类型: {addr.RegisterType}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return WriteResult.Failed(tag.Id, value, $"写入通信失败: {ex.Message}", sw.ElapsedMilliseconds);
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
        var results = new Dictionary<long, WriteResult>();
        foreach (var kv in writes)
        {
            var res = await WriteTagAsync(kv.Key, kv.Value, ct);
            results[kv.Key.Id] = res;
        }
        return results;
    }

    #region 地址合并打包算法 (Packet Packing)

    private sealed class ReadBlock
    {
        public ushort StartAddress { get; set; }
        public ushort Count { get; set; }
        public List<(TagNode Tag, ModbusAddress Address)> Items { get; } = new();
    }

    private static List<ReadBlock> PackItemsIntoBlocks(
        List<(TagNode Tag, ModbusAddress Address)> items, 
        bool isBitArea, 
        int maxBlockLimit, 
        int maxGap)
    {
        var sorted = items.OrderBy(x => x.Address.StartAddress).ToList();
        var blocks = new List<ReadBlock>();
        ReadBlock? current = null;

        foreach (var item in sorted)
        {
            var start = item.Address.StartAddress;
            var count = item.Address.RegisterCount;
            var end = start + count;

            if (current == null)
            {
                current = new ReadBlock
                {
                    StartAddress = start,
                    Count = count
                };
                current.Items.Add(item);
                blocks.Add(current);
                continue;
            }

            var potentialEnd = Math.Max(current.StartAddress + current.Count, end);
            var potentialSpan = potentialEnd - current.StartAddress;
            var gap = start - (current.StartAddress + current.Count);

            if (potentialSpan <= maxBlockLimit && gap <= maxGap)
            {
                current.Count = (ushort)(potentialEnd - current.StartAddress);
                current.Items.Add(item);
            }
            else
            {
                current = new ReadBlock
                {
                    StartAddress = start,
                    Count = count
                };
                current.Items.Add(item);
                blocks.Add(current);
            }
        }

        return blocks;
    }

    private static void DistributeReadResults(
        ReadBlock block,
        ReadOnlyMemory<byte> dataMemory,
        bool isBitArea,
        ModbusEndian endian,
        Dictionary<long, TagValueSnapshot> results,
        DateTime sampleTime)
    {
        var dataSpan = dataMemory.Span;
        foreach (var (tag, addr) in block.Items)
        {
            if (isBitArea)
            {
                var bitOffset = addr.StartAddress - block.StartAddress;
                var byteIdx = bitOffset / 8;
                var bitInByte = bitOffset % 8;

                if (byteIdx < dataSpan.Length)
                {
                    var bitVal = ((dataSpan[byteIdx] >> bitInByte) & 1) == 1;
                    results[tag.Id] = new TagValueSnapshot
                    {
                        TagId = tag.Id,
                        Value = bitVal,
                        RawValue = bitVal,
                        Quality = QualityCode.Good,
                        Timestamp = sampleTime
                    };
                }
                else
                {
                    results[tag.Id] = new TagValueSnapshot
                    {
                        TagId = tag.Id,
                        Quality = QualityCode.Bad,
                        Timestamp = sampleTime
                    };
                }
            }
            else
            {
                var regOffset = addr.StartAddress - block.StartAddress;
                var byteOffset = regOffset * 2;
                var neededBytes = addr.RegisterCount * 2;

                if (byteOffset + neededBytes <= dataSpan.Length)
                {
                    var slice = dataSpan.Slice(byteOffset, neededBytes);
                    var (val, raw) = ModbusDataConverter.DecodeValue(
                        slice, 
                        tag, 
                        endian, 
                        addr.BitIndex);

                    results[tag.Id] = new TagValueSnapshot
                    {
                        TagId = tag.Id,
                        Value = val,
                        RawValue = raw,
                        Quality = QualityCode.Good,
                        Timestamp = sampleTime
                    };
                }
                else
                {
                    results[tag.Id] = new TagValueSnapshot
                    {
                        TagId = tag.Id,
                        Quality = QualityCode.Bad,
                        Timestamp = sampleTime
                    };
                }
            }
        }
    }

    private static ushort PrepareBitInRegisterWrite(
        ReadOnlyMemory<byte> readBytes, 
        int bitIndex, 
        bool bitVal, 
        ModbusEndian endian)
    {
        var currentReg = ModbusDataConverter.ReadUInt16(readBytes.Span, endian);
        ushort modifiedReg = bitVal 
            ? (ushort)(currentReg | (1 << bitIndex)) 
            : (ushort)(currentReg & ~(1 << bitIndex));

        var writeBytes = ModbusDataConverter.WriteUInt16(modifiedReg, endian);
        return BinaryPrimitives.ReadUInt16BigEndian(writeBytes);
    }

    private static IModbusFramer SelectFramer(ProtocolType protocolType) => protocolType switch
    {
        ProtocolType.ModbusRtu => new RtuFramer(),
        ProtocolType.ModbusAscii => new AsciiFramer(),
        _ => new MbapFramer()
    };

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

/// <summary>
/// Modbus TCP 驱动具体类型 (默认以太网端口 502)
/// </summary>
public class ModbusTcpDriver : ModbusDriver
{
    public ModbusTcpDriver() : base(new MbapFramer()) { }

    public override ProtocolType ProtocolType => ProtocolType.ModbusTcp;
    public override string ProtocolName => "ModbusTcp";
}

/// <summary>
/// Modbus RTU 驱动具体类型 (串口 RS485/RS232 带 CRC16)
/// </summary>
public class ModbusRtuDriver : ModbusDriver
{
    public ModbusRtuDriver() : base(new RtuFramer()) { }

    public override ProtocolType ProtocolType => ProtocolType.ModbusRtu;
    public override string ProtocolName => "ModbusRtu";
}

/// <summary>
/// Modbus ASCII 驱动具体类型 (串口冒号起始带 LRC)
/// </summary>
public class ModbusAsciiDriver : ModbusDriver
{
    public ModbusAsciiDriver() : base(new AsciiFramer()) { }

    public override ProtocolType ProtocolType => ProtocolType.ModbusAscii;
    public override string ProtocolName => "ModbusAscii";
}
