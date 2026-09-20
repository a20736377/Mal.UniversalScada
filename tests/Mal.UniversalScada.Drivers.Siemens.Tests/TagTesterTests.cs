using System.Buffers.Binary;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Configuration;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.Siemens.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.Siemens.Tests;

/// <summary>
/// 模拟通道工厂，返回 MockS7Channel
/// </summary>
internal class MockChannelFactory : IChannelFactory
{
    private readonly MockS7Channel _channel;
    public MockChannelFactory(MockS7Channel channel) => _channel = channel;
    public IChannel CreateChannel(ChannelConfig config) => _channel;
}

public class TagTesterTests
{
    static TagTesterTests()
    {
        DefaultDriverFactory.RegisterDriver<SiemensS7Driver>(ProtocolType.SiemensS7, "SiemensS7");
    }
    [Fact]
    public async Task TestReadTagAsync_WhenDeviceAndChannelValid_ReturnsGoodSnapshot()
    {
        var mockChannel = new MockS7Channel();
        var channelFactory = new MockChannelFactory(mockChannel);
        var driverFactory = new DefaultDriverFactory(null!);

        var tester = new DefaultTagTester(driverFactory, channelFactory);

        var channel = new ChannelConfig
        {
            ChannelId = "CH1",
            ChannelType = ChannelType.TcpClient,
            Host = "127.0.0.1",
            Port = 102
        };

        var device = new DeviceNode
        {
            DeviceId = "DEV1",
            ChannelId = "CH1",
            ProtocolType = ProtocolType.SiemensS7,
            StationAddress = 1,
            TimeoutMs = 1000
        };

        var tag = new TagNode
        {
            Id = 1,
            DeviceId = "DEV1",
            Address = "DB1.DBD0",
            DataType = TagDataType.Float
        };

        // 1. 预置握手 COTP CC
        mockChannel.EnqueueResponse([0x03, 0x00, 0x00, 0x07, 0x02, 0xD0, 0x00]);

        // 2. 预置 S7 Setup Ack
        var setupAckPdu = new byte[12 + 8];
        setupAckPdu[0] = 0x32;
        setupAckPdu[1] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(setupAckPdu.AsSpan(6, 2), 8);
        setupAckPdu[12] = 0xF0;
        BinaryPrimitives.WriteUInt16BigEndian(setupAckPdu.AsSpan(18, 2), 480);
        mockChannel.EnqueueResponse(CotpHandler.WrapDataPdu(setupAckPdu));

        // 3. 预置 S7 Read Var Ack (Float 50.5f)
        var readAckPdu = new byte[12 + 2 + (4 + 4)];
        readAckPdu[0] = 0x32;
        readAckPdu[1] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(readAckPdu.AsSpan(6, 2), 2);
        BinaryPrimitives.WriteUInt16BigEndian(readAckPdu.AsSpan(8, 2), 8);
        readAckPdu[12] = 0x04;
        readAckPdu[13] = 1;
        readAckPdu[14] = 0xFF; // Success
        readAckPdu[15] = 0x04; // Byte
        BinaryPrimitives.WriteUInt16BigEndian(readAckPdu.AsSpan(16, 2), 32); // 32 bits

        // 50.5f in IEEE 754 Big-Endian: 0x42, 0x4A, 0x00, 0x00
        readAckPdu[18] = 0x42;
        readAckPdu[19] = 0x4A;
        readAckPdu[20] = 0x00;
        readAckPdu[21] = 0x00;
        mockChannel.EnqueueResponse(CotpHandler.WrapDataPdu(readAckPdu));

        var result = await tester.TestReadTagAsync(tag, device, channel);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(QualityCode.Good, result.Quality);
        Assert.NotNull(result.Value);
        Assert.InRange(Convert.ToSingle(result.Value), 50.49f, 50.51f);
    }

    [Fact]
    public async Task TestWriteTagAsync_WhenTagIsReadOnly_RejectsImmediately()
    {
        var mockChannel = new MockS7Channel();
        var channelFactory = new MockChannelFactory(mockChannel);
        var driverFactory = new DefaultDriverFactory(null!);

        var tester = new DefaultTagTester(driverFactory, channelFactory);

        var channel = new ChannelConfig { ChannelId = "CH1" };
        var device = new DeviceNode { DeviceId = "DEV1", ProtocolType = ProtocolType.SiemensS7 };
        var tag = new TagNode
        {
            Id = 1,
            DeviceId = "DEV1",
            Address = "DB1.DBD0",
            DataType = TagDataType.Float,
            AccessMode = TagAccessMode.ReadOnly
        };

        var result = await tester.TestWriteTagAsync(tag, "100", device, channel);

        Assert.False(result.IsSuccess);
        Assert.Contains("只读", result.Message);
    }
}
