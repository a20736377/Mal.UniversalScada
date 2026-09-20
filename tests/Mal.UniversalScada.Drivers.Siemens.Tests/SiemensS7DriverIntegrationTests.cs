using System.Buffers.Binary;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.Siemens.Enums;
using Mal.UniversalScada.Drivers.Siemens.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.Siemens.Tests;

/// <summary>
/// 模拟仿真西门子 PLC 响应的内存通道，用于端到端自动化测试驱动闭环
/// </summary>
internal class MockS7Channel : IChannel
{
    private readonly Queue<byte[]> _responses = new();
    private readonly List<byte[]> _sentPackets = new();

    public string ChannelId => "Mock_S7_Channel";
    public ChannelState State { get; private set; } = ChannelState.Connected;
    public bool IsOpen => State == ChannelState.Connected;

#pragma warning disable CS0067
    public event EventHandler<ChannelState>? StateChanged;
#pragma warning restore CS0067

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
                throw new TimeoutException("MockS7Channel 中无预置的待接收应答报文");
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

public class SiemensS7DriverIntegrationTests
{
    [Fact]
    public async Task SiemensS7Driver_EndToEnd_Initialize_Read_Write_Success()
    {
        using var driver = new SiemensS7Driver();
        var mockChannel = new MockS7Channel();

        var device = new DeviceNode
        {
            DeviceId = "PLC1",
            ProtocolType = ProtocolType.SiemensS7,
            StationAddress = 1, // S7-1200 / S7-1500 (Rack 0, Slot 1)
            TimeoutMs = 1000
        };

        // 1. 预置步骤一 COTP CC 应答帧: TPKT (0x03 0x00 0x00 0x07) + COTP CC (0x02 0xD0 0x00)
        byte[] cotpCcPacket = [0x03, 0x00, 0x00, 0x07, 0x02, 0xD0, 0x00];
        mockChannel.EnqueueResponse(cotpCcPacket);

        // 2. 预置步骤二 S7 Setup Comm Ack 应答帧 (协商 PDU = 480)
        var setupAckPdu = new byte[12 + 8];
        setupAckPdu[0] = 0x32;
        setupAckPdu[1] = 0x03; // Ack-Data
        BinaryPrimitives.WriteUInt16BigEndian(setupAckPdu.AsSpan(6, 2), 8); // param len
        setupAckPdu[12] = 0xF0; // Func
        BinaryPrimitives.WriteUInt16BigEndian(setupAckPdu.AsSpan(18, 2), 480); // Negotiated Pdu = 480
        mockChannel.EnqueueResponse(CotpHandler.WrapDataPdu(setupAckPdu));

        // 执行握手
        var initOk = await driver.InitializeAsync(mockChannel, device);
        Assert.True(initOk);
        Assert.True(driver.IsInitialized);
        Assert.Equal((ushort)480, driver.NegotiatedPduLength);

        // 3. 预置 S7 批量读取应答帧:
        // Tag 1 (DB1.DBD0 Float 123.456)
        // Tag 2 (M0.0 Bool True)
        var readAckPdu = new byte[12 + 2 + (4 + 4) + (4 + 1)];
        readAckPdu[0] = 0x32;
        readAckPdu[1] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(readAckPdu.AsSpan(6, 2), 2);
        BinaryPrimitives.WriteUInt16BigEndian(readAckPdu.AsSpan(8, 2), (ushort)(readAckPdu.Length - 14));
        readAckPdu[12] = 0x04; // Read Var
        readAckPdu[13] = 2;

        int cur = 14;
        // Item 1: Float 123.456
        readAckPdu[cur++] = 0xFF; // Success
        readAckPdu[cur++] = 0x04; // Byte
        BinaryPrimitives.WriteUInt16BigEndian(readAckPdu.AsSpan(cur, 2), 32);
        cur += 2;
        readAckPdu[cur++] = 0x42;
        readAckPdu[cur++] = 0xF6;
        readAckPdu[cur++] = 0xE9;
        readAckPdu[cur++] = 0x79;

        // Item 2: Bool True
        readAckPdu[cur++] = 0xFF; // Success
        readAckPdu[cur++] = 0x03; // Bit
        BinaryPrimitives.WriteUInt16BigEndian(readAckPdu.AsSpan(cur, 2), 1);
        cur += 2;
        readAckPdu[cur++] = 0x01;

        mockChannel.EnqueueResponse(CotpHandler.WrapDataPdu(readAckPdu));

        var tag1 = new TagNode { Id = 1, Address = "DB1.DBD0", DataType = TagDataType.Float };
        var tag2 = new TagNode { Id = 2, Address = "M0.0", DataType = TagDataType.Bool };

        var readResult = await driver.ReadBatchAsync([tag1, tag2]);

        Assert.Equal(2, readResult.Count);
        Assert.Equal(QualityCode.Good, readResult[1].Quality);
        Assert.InRange(Convert.ToSingle(readResult[1].Value), 123.455f, 123.457f);

        Assert.Equal(QualityCode.Good, readResult[2].Quality);
        Assert.Equal(true, readResult[2].Value);

        // 4. 预置 S7 写入应答帧 (Write Var 0x05)
        var writeAckPdu = new byte[12 + 2 + 1];
        writeAckPdu[0] = 0x32;
        writeAckPdu[1] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(writeAckPdu.AsSpan(6, 2), 2);
        BinaryPrimitives.WriteUInt16BigEndian(writeAckPdu.AsSpan(8, 2), 1);
        writeAckPdu[12] = 0x05; // Write Var
        writeAckPdu[13] = 1;    // Item count
        writeAckPdu[14] = 0xFF; // Success

        mockChannel.EnqueueResponse(CotpHandler.WrapDataPdu(writeAckPdu));

        var writeRes = await driver.WriteTagAsync(tag1, 999.0f);
        Assert.True(writeRes.IsSuccess);
        Assert.Equal(1, writeRes.TagId);
    }
}
