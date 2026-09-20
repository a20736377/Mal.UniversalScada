using System.Buffers.Binary;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.CustomSerial.Enums;
using Mal.UniversalScada.Drivers.CustomSerial.Models;
using Mal.UniversalScada.Drivers.CustomSerial.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.CustomSerial.Tests;

internal class MockSerialChannel : IChannel
{
    private readonly Queue<byte[]> _responses = new();
    private readonly List<byte[]> _sentPackets = new();

    public string ChannelId => "Mock_Serial_Channel";
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
                throw new TimeoutException("MockSerialChannel 中无待接收应答数据");
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

public class CustomSerialDriverIntegrationTests
{
    [Fact]
    public async Task CustomSerialDriver_BinaryMode_ReadAndWrite_Success()
    {
        using var driver = new CustomSerialDriver();
        var mockChannel = new MockSerialChannel();

        var device = new DeviceNode
        {
            DeviceId = "MCU_Board_1",
            ProtocolType = ProtocolType.CustomSerial,
            StationAddress = 1,
            CustomProtocolName = "Header=AA55;Tail=0D0A;Check=Sum8",
            TimeoutMs = 1000
        };

        var initOk = await driver.InitializeAsync(mockChannel, device);
        Assert.True(initOk);

        var tags = new List<TagNode>
        {
            new() { Id = 1, Address = "CMD01.0", DataType = TagDataType.Int16 },
            new() { Id = 2, Address = "CMD01.2", DataType = TagDataType.Int16 }
        };

        // 构造单片机返回数据: Temp=250 (0x00FA), Pressure=1013 (0x03F5)
        byte[] payload = [0x00, 0xFA, 0x03, 0xF5];
        var respFrame = CustomSerialCodec.BuildBinaryRequest(driver.Config, 0x01, payload);
        mockChannel.EnqueueResponse(respFrame);

        var readResults = await driver.ReadBatchAsync(tags);

        Assert.Equal(QualityCode.Good, readResults[1].Quality);
        Assert.Equal((short)250, readResults[1].Value);
        Assert.Equal((short)1013, readResults[2].Value);

        // 测试写入: 写 CMD01.0
        var writeTag = new TagNode
        {
            Id = 3,
            Address = "CMD01.0",
            DataType = TagDataType.Int16,
            AccessMode = TagAccessMode.ReadWrite
        };

        // 写入应答
        var writeResp = CustomSerialCodec.BuildBinaryRequest(driver.Config, 0x05, [0x00]);
        mockChannel.EnqueueResponse(writeResp);

        var writeResult = await driver.WriteTagAsync(writeTag, (short)500);
        Assert.True(writeResult.IsSuccess);
    }

    [Fact]
    public async Task CustomSerialDriver_AsciiMode_ReadLine_Success()
    {
        using var driver = new CustomSerialDriver();
        var mockChannel = new MockSerialChannel();

        var device = new DeviceNode
        {
            DeviceId = "Weight_Scale",
            ProtocolType = ProtocolType.CustomSerial,
            CustomProtocolName = "Mode=AsciiLine",
            TimeoutMs = 1000
        };

        await driver.InitializeAsync(mockChannel, device);

        var tag = new TagNode
        {
            Id = 4,
            Address = "INDEX1",
            DataType = TagDataType.Float
        };

        // 模拟电子秤输出: "WEIGHT,45.67,KG\r\n"
        var asciiBytes = System.Text.Encoding.ASCII.GetBytes("WEIGHT,45.67,KG\r\n");
        mockChannel.EnqueueResponse(asciiBytes);

        var results = await driver.ReadBatchAsync([tag]);

        Assert.Equal(QualityCode.Good, results[4].Quality);
        Assert.Equal(45.67, Convert.ToDouble(results[4].Value), 2);
    }
}
