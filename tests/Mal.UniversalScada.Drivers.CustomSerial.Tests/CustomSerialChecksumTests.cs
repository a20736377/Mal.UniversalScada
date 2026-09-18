using Mal.UniversalScada.Drivers.CustomSerial.Enums;
using Mal.UniversalScada.Drivers.CustomSerial.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.CustomSerial.Tests;

public class CustomSerialChecksumTests
{
    [Fact]
    public void ComputeSum8_CalculatesCorrectSum()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04];
        var sum = CustomSerialChecksum.ComputeSum8(data);
        Assert.Equal(10, sum);

        // 溢出 256 测试: 0xFE + 0x05 = 0x103 -> 0x03
        byte[] overflow = [0xFE, 0x05];
        Assert.Equal(0x03, CustomSerialChecksum.ComputeSum8(overflow));
    }

    [Fact]
    public void ComputeXor8_CalculatesCorrectBcc()
    {
        byte[] data = [0xAA, 0x55, 0x01];
        // 0xAA ^ 0x55 = 0xFF, 0xFF ^ 0x01 = 0xFE
        var xor = CustomSerialChecksum.ComputeXor8(data);
        Assert.Equal(0xFE, xor);
    }

    [Fact]
    public void Validate_MatchingChecksum_ReturnsTrue()
    {
        byte[] data = [0x01, 0x02, 0x03];
        byte[] check = [0x06];

        Assert.True(CustomSerialChecksum.Validate(CustomSerialCheckType.Sum8, data, check));

        byte[] wrongCheck = [0x07];
        Assert.False(CustomSerialChecksum.Validate(CustomSerialCheckType.Sum8, data, wrongCheck));
    }
}
