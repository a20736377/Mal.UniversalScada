using Mal.UniversalScada.Drivers.Modbus.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.Modbus.Tests;

public class ModbusCrcAndLrcTests
{
    [Fact]
    public void ComputeCrc_KnownRequest_ReturnsCorrectCrc()
    {
        // 测试帧: 01 03 00 00 00 01 (从站 1 读保持寄存器 0 数量 1)
        // 期望 CRC 低字节 0x84, 高字节 0x0A (0x0A84)
        byte[] frame = [0x01, 0x03, 0x00, 0x00, 0x00, 0x01];
        var crc = ModbusCrc.Compute(frame);

        var low = (byte)(crc & 0xFF);
        var high = (byte)((crc >> 8) & 0xFF);

        Assert.Equal(0x84, low);
        Assert.Equal(0x0A, high);
    }

    [Fact]
    public void ValidateCrc_ValidAndCorruptFrame_ReturnsExpected()
    {
        // 完整有效帧 (低位在前，高位在后)
        byte[] validFrame = [0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x84, 0x0A];
        Assert.True(ModbusCrc.Validate(validFrame));

        // 篡改 1 字节
        byte[] corruptFrame = [0x01, 0x03, 0x00, 0x00, 0x00, 0x02, 0x84, 0x0A];
        Assert.False(ModbusCrc.Validate(corruptFrame));
    }

    [Fact]
    public void AppendCrc_ValidData_AppendsLowByteFirst()
    {
        byte[] data = [0x01, 0x03, 0x00, 0x00, 0x00, 0x01];
        var full = new byte[8];
        data.CopyTo(full, 0);

        ModbusCrc.AppendCrc(full, data);

        Assert.Equal(0x84, full[6]);
        Assert.Equal(0x0A, full[7]);
        Assert.True(ModbusCrc.Validate(full));
    }

    [Fact]
    public void ComputeLrc_StandardExample_ReturnsCorrectLrc()
    {
        // 示例: 01 03 00 02 00 02 -> 和为 0x08 -> 补码 0xF8
        byte[] buffer = [0x01, 0x03, 0x00, 0x02, 0x00, 0x02];
        var lrc = ModbusLrc.Compute(buffer);
        Assert.Equal(0xF8, lrc);
    }

    [Fact]
    public void EncodeAndDecodeAsciiFrame_Roundtrip_Succeeds()
    {
        byte slaveId = 1;
        byte[] pdu = [0x03, 0x00, 0x02, 0x00, 0x02];

        var encoded = ModbusLrc.EncodeAsciiFrame(slaveId, pdu);
        var str = System.Text.Encoding.ASCII.GetString(encoded);

        // 格式应当是: ":010300020002F8\r\n"
        Assert.StartsWith(":", str);
        Assert.EndsWith("\r\n", str);
        Assert.Contains("010300020002F8", str);

        var success = ModbusLrc.TryDecodeAsciiFrame(encoded, out var outSlave, out var outPdu, out var error);
        Assert.True(success, error);
        Assert.Equal(slaveId, outSlave);
        Assert.Equal(pdu, outPdu);
    }
}
