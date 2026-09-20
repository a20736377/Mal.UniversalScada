using System.Buffers.Binary;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.Modbus.Enums;
using Mal.UniversalScada.Drivers.Modbus.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.Modbus.Tests;

internal class MockModbusChannel : IChannel
{
    private readonly Queue<byte[]> _responses = new();
    private readonly List<byte[]> _sentPackets = new();

    public string ChannelId => "Mock_Modbus_Channel";
    public ChannelState State { get; private set; } = ChannelState.Connected;
    public bool IsOpen => State == ChannelState.Connected;

#pragma warning disable CS0067
    public event EventHandler<ChannelState>? StateChanged;
#pragma warning restore CS0067

    public IReadOnlyList<byte[]> SentPackets => _sentPackets;

    public Task<bool> OpenAsync(CancellationToken ct = default)
    {
        State = ChannelState.Connected;
        return Task.FromResult(true);
    }

    public Task CloseAsync()
    {
        State = ChannelState.Disconnected;
        return Task.CompletedTask;
    }

    public void EnqueueResponse(byte[] response)
    {
        _responses.Enqueue(response);
    }

    public Task<int> SendAsync(byte[] buffer, int offset, int count, CancellationToken ct = default)
    {
        var sent = new byte[count];
        Buffer.BlockCopy(buffer, offset, sent, 0, count);
        _sentPackets.Add(sent);
        return Task.FromResult(count);
    }

    private byte[]? _currentResp;
    private int _currentRespOffset;

    public Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken ct = default)
    {
        if (_currentResp == null || _currentRespOffset >= _currentResp.Length)
        {
            if (_responses.Count == 0)
            {
                throw new TimeoutException("MockModbusChannel 中无预置的待接收应答报文");
            }
            _currentResp = _responses.Dequeue();
            _currentRespOffset = 0;
        }

        int available = _currentResp.Length - _currentRespOffset;
        int toCopy = Math.Min(count, available);
        Buffer.BlockCopy(_currentResp, _currentRespOffset, buffer, offset, toCopy);
        _currentRespOffset += toCopy;

        return Task.FromResult(toCopy);
    }

    public void ClearBuffer()
    {
        _currentResp = null;
        _currentRespOffset = 0;
    }

    public void Dispose() => CloseAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class ModbusDriverIntegrationTests
{
    [Fact]
    public async Task ModbusTcpDriver_PacketPacking_BatchRead_Success()
    {
        using var driver = new ModbusTcpDriver();
        var mockChannel = new MockModbusChannel();

        var device = new DeviceNode
        {
            DeviceId = "DEV1",
            ProtocolType = ProtocolType.ModbusTcp,
            StationAddress = 1,
            TimeoutMs = 1000
        };

        var initSuccess = await driver.InitializeAsync(mockChannel, device);
        Assert.True(initSuccess);
        Assert.True(driver.IsInitialized);

        // 组态 3 个连续的保持寄存器点位 (40001, 40002, 40003)
        var tags = new List<TagNode>
        {
            new() { Id = 1, Address = "40001", DataType = TagDataType.Int16 },
            new() { Id = 2, Address = "40002", DataType = TagDataType.Int16 },
            new() { Id = 3, Address = "40003", DataType = TagDataType.Int16 }
        };

        // 准备下位机响应: TxId=1, Proto=0, Len=9, UnitId=1, FC=3, ByteCount=6, Data=[100, 200, 300]
        var mbapResp = new byte[]
        {
            0x00, 0x01, // TxId = 1
            0x00, 0x00, // Proto = 0
            0x00, 0x09, // Length = 9 (1 byte UnitId + 1 byte FC + 1 byte ByteCount + 6 bytes data)
            0x01,       // UnitId = 1
            0x03,       // FC = 3
            0x06,       // ByteCount = 6
            0x00, 0x64, // 100
            0x00, 0xC8, // 200
            0x01, 0x2C  // 300
        };
        mockChannel.EnqueueResponse(mbapResp);

        var batchResults = await driver.ReadBatchAsync(tags);

        // 验证只发送了 1 次请求报文 (Packet Packing 连续合并)
        Assert.Single(mockChannel.SentPackets);
        var sentPdu = mockChannel.SentPackets[0];
        // 验证请求起始地址为 0，数量为 3
        Assert.Equal(0x03, sentPdu[7]); // FC
        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(sentPdu.AsSpan(8, 2))); // StartAddr 0
        Assert.Equal(3, BinaryPrimitives.ReadUInt16BigEndian(sentPdu.AsSpan(10, 2))); // Quantity 3

        Assert.Equal(3, batchResults.Count);
        Assert.Equal(QualityCode.Good, batchResults[1].Quality);
        Assert.Equal((short)100, batchResults[1].Value);
        Assert.Equal((short)200, batchResults[2].Value);
        Assert.Equal((short)300, batchResults[3].Value);
    }

    [Fact]
    public async Task ModbusTcpDriver_WriteSingleRegister_Success()
    {
        using var driver = new ModbusTcpDriver();
        var mockChannel = new MockModbusChannel();

        var device = new DeviceNode
        {
            DeviceId = "DEV1",
            ProtocolType = ProtocolType.ModbusTcp,
            StationAddress = 1,
            TimeoutMs = 1000
        };

        await driver.InitializeAsync(mockChannel, device);

        var tag = new TagNode
        {
            Id = 10,
            Address = "40005", // 偏移 4
            DataType = TagDataType.Int16,
            AccessMode = TagAccessMode.ReadWrite
        };

        // FC06 回声应答: TxId 1, Len 6, UnitId 1, FC 6, Addr 4, Val 555 (0x022B)
        var mbapResp = new byte[]
        {
            0x00, 0x01,
            0x00, 0x00,
            0x00, 0x06,
            0x01,
            0x06,
            0x00, 0x04,
            0x02, 0x2B
        };
        mockChannel.EnqueueResponse(mbapResp);

        var writeResult = await driver.WriteTagAsync(tag, (short)555);

        Assert.True(writeResult.IsSuccess);
        Assert.Equal((short)555, writeResult.TargetValue);
    }

    [Fact]
    public async Task ModbusRtuDriver_ReadCoilAndWriteCoil_Success()
    {
        using var driver = new ModbusRtuDriver();
        var mockChannel = new MockModbusChannel();

        var device = new DeviceNode
        {
            DeviceId = "DEV_RTU",
            ProtocolType = ProtocolType.ModbusRtu,
            StationAddress = 1,
            TimeoutMs = 1000
        };

        await driver.InitializeAsync(mockChannel, device);

        var tag = new TagNode
        {
            Id = 20,
            Address = "00001", // 线圈 0
            DataType = TagDataType.Bool,
            AccessMode = TagAccessMode.ReadWrite
        };

        // 1. 测试读线圈 (FC01)
        // 期望响应: [Slave 1, FC 1, ByteCount 1, Status 0x01, CRC_LO, CRC_HI]
        var readRespData = new byte[] { 0x01, 0x01, 0x01, 0x01 };
        var readRespFull = new byte[6];
        readRespData.CopyTo(readRespFull, 0);
        ModbusCrc.AppendCrc(readRespFull, readRespData);
        mockChannel.EnqueueResponse(readRespFull);

        var readResults = await driver.ReadBatchAsync([tag]);
        Assert.Equal(true, readResults[20].Value);
        Assert.Equal(QualityCode.Good, readResults[20].Quality);

        // 2. 测试写线圈 (FC05)
        // 期望响应: [Slave 1, FC 5, Addr_Hi 0, Addr_Lo 0, Val_Hi 0xFF, Val_Lo 0x00, CRC_LO, CRC_HI]
        var writeRespData = new byte[] { 0x01, 0x05, 0x00, 0x00, 0xFF, 0x00 };
        var writeRespFull = new byte[8];
        writeRespData.CopyTo(writeRespFull, 0);
        ModbusCrc.AppendCrc(writeRespFull, writeRespData);
        mockChannel.EnqueueResponse(writeRespFull);

        var writeResult = await driver.WriteTagAsync(tag, true);
        Assert.True(writeResult.IsSuccess);
    }

    [Fact]
    public async Task ModbusAsciiDriver_ReadHoldingRegister_Success()
    {
        using var driver = new ModbusAsciiDriver();
        var mockChannel = new MockModbusChannel();

        var device = new DeviceNode
        {
            DeviceId = "DEV_ASCII",
            ProtocolType = ProtocolType.ModbusAscii,
            StationAddress = 1,
            TimeoutMs = 1000
        };

        await driver.InitializeAsync(mockChannel, device);

        var tag = new TagNode
        {
            Id = 30,
            Address = "40001",
            DataType = TagDataType.Int16
        };

        // 响应 PDU: [FC 3, ByteCount 2, Val 0x00 0x7B] (123)
        byte[] pdu = [0x03, 0x02, 0x00, 0x7B];
        var asciiFrame = ModbusLrc.EncodeAsciiFrame(1, pdu);
        mockChannel.EnqueueResponse(asciiFrame);

        var results = await driver.ReadBatchAsync([tag]);
        Assert.Equal((short)123, results[30].Value);
        Assert.Equal(QualityCode.Good, results[30].Quality);
    }
}
