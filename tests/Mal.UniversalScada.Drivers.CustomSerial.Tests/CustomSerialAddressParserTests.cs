using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Drivers.CustomSerial.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.CustomSerial.Tests;

public class CustomSerialAddressParserTests
{
    [Theory]
    [InlineData("CMD01.0", 0x01, 0, null)]
    [InlineData("CMD03.4", 0x03, 4, null)]
    [InlineData("CMD01.2.5", 0x01, 2, 5)]
    [InlineData("02.10", 0x02, 10, null)]
    public void Parse_CommandAndOffset_ReturnsExpected(string raw, byte expectedCmd, int expectedOffset, int? expectedBit)
    {
        var addr = CustomSerialAddressParser.Parse(raw, TagDataType.Int16);
        Assert.Equal(expectedCmd, addr.Command);
        Assert.Equal(expectedOffset, addr.ByteOffset);
        Assert.Equal(expectedBit, addr.BitIndex);
    }

    [Theory]
    [InlineData("REG0", 0, null)]
    [InlineData("REG1", 2, null)]
    [InlineData("D5", 10, null)]
    [InlineData("W2.3", 4, 3)]
    public void Parse_RegisterAlias_ReturnsWordOffset(string raw, int expectedOffset, int? expectedBit)
    {
        var addr = CustomSerialAddressParser.Parse(raw, TagDataType.Int16);
        Assert.Equal(expectedOffset, addr.ByteOffset);
        Assert.Equal(expectedBit, addr.BitIndex);
    }

    [Theory]
    [InlineData("INDEX0", 0)]
    [InlineData("FIELD2", 2)]
    [InlineData("CSV3", 3)]
    public void Parse_AsciiFieldIndex_ReturnsFieldIndex(string raw, int expectedIdx)
    {
        var addr = CustomSerialAddressParser.Parse(raw, TagDataType.Float);
        Assert.Equal(expectedIdx, addr.FieldIndex);
    }
}
