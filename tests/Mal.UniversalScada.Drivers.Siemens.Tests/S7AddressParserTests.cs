using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Drivers.Siemens.Enums;
using Mal.UniversalScada.Drivers.Siemens.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.Siemens.Tests;

public class S7AddressParserTests
{
    [Theory]
    [InlineData("DB1.DBD0", TagDataType.Float, 1, 0, 0, 4, false)]
    [InlineData("DB2.DBW4", TagDataType.Int16, 2, 4, 0, 2, false)]
    [InlineData("DB3.DBB10", TagDataType.UInt8, 3, 10, 0, 1, false)]
    [InlineData("DB1.DBX0.5", TagDataType.Bool, 1, 0, 5, 1, true)]
    [InlineData("DB5.20.7", TagDataType.Bool, 5, 20, 7, 1, true)]
    [InlineData("DB10.100", TagDataType.Int32, 10, 100, 0, 4, false)]
    public void Parse_DbAddresses_CorrectlyParsed(
        string raw, TagDataType dataType, int expectedDb, int expectedStart, byte expectedBit, int expectedLen, bool expectedIsBit)
    {
        var addr = S7AddressParser.Parse(raw, dataType);

        Assert.Equal(S7AreaCode.DataBlock, addr.Area);
        Assert.Equal(expectedDb, addr.DbNumber);
        Assert.Equal(expectedStart, addr.StartByte);
        Assert.Equal(expectedBit, addr.BitIndex);
        Assert.Equal(expectedLen, addr.ByteLength);
        Assert.Equal(expectedIsBit, addr.IsBit);
    }

    [Theory]
    [InlineData("M0.0", TagDataType.Bool, 0, 0, 1, true)]
    [InlineData("M10.7", TagDataType.Bool, 10, 7, 1, true)]
    [InlineData("MB2", TagDataType.UInt8, 2, 0, 1, false)]
    [InlineData("MW4", TagDataType.Int16, 4, 0, 2, false)]
    [InlineData("MD8", TagDataType.Float, 8, 0, 4, false)]
    public void Parse_MerkerAddresses_CorrectlyParsed(
        string raw, TagDataType dataType, int expectedStart, byte expectedBit, int expectedLen, bool expectedIsBit)
    {
        var addr = S7AddressParser.Parse(raw, dataType);

        Assert.Equal(S7AreaCode.Merker, addr.Area);
        Assert.Equal(0, addr.DbNumber);
        Assert.Equal(expectedStart, addr.StartByte);
        Assert.Equal(expectedBit, addr.BitIndex);
        Assert.Equal(expectedLen, addr.ByteLength);
        Assert.Equal(expectedIsBit, addr.IsBit);
    }

    [Theory]
    [InlineData("I0.0", TagDataType.Bool, 0, 0, true)]
    [InlineData("E0.1", TagDataType.Bool, 0, 1, true)]
    [InlineData("IW2", TagDataType.Int16, 2, 0, false)]
    [InlineData("ED4", TagDataType.Int32, 4, 0, false)]
    public void Parse_InputAddresses_CorrectlyParsed(
        string raw, TagDataType dataType, int expectedStart, byte expectedBit, bool expectedIsBit)
    {
        var addr = S7AddressParser.Parse(raw, dataType);

        Assert.Equal(S7AreaCode.Inputs, addr.Area);
        Assert.Equal(expectedStart, addr.StartByte);
        Assert.Equal(expectedBit, addr.BitIndex);
        Assert.Equal(expectedIsBit, addr.IsBit);
    }

    [Theory]
    [InlineData("Q0.0", TagDataType.Bool, 0, 0, true)]
    [InlineData("A0.5", TagDataType.Bool, 0, 5, true)]
    [InlineData("QW0", TagDataType.UInt16, 0, 0, false)]
    [InlineData("QD4", TagDataType.Float, 4, 0, false)]
    public void Parse_OutputAddresses_CorrectlyParsed(
        string raw, TagDataType dataType, int expectedStart, byte expectedBit, bool expectedIsBit)
    {
        var addr = S7AddressParser.Parse(raw, dataType);

        Assert.Equal(S7AreaCode.Outputs, addr.Area);
        Assert.Equal(expectedStart, addr.StartByte);
        Assert.Equal(expectedBit, addr.BitIndex);
        Assert.Equal(expectedIsBit, addr.IsBit);
    }

    [Theory]
    [InlineData("V0.0", TagDataType.Bool, 0, 0, true)]
    [InlineData("VB10", TagDataType.UInt8, 10, 0, false)]
    [InlineData("VW20", TagDataType.Int16, 20, 0, false)]
    [InlineData("VD100", TagDataType.Float, 100, 0, false)]
    public void Parse_VAreaAddresses_MappedToDb1(
        string raw, TagDataType dataType, int expectedStart, byte expectedBit, bool expectedIsBit)
    {
        var addr = S7AddressParser.Parse(raw, dataType);

        Assert.Equal(S7AreaCode.DataBlock, addr.Area);
        Assert.Equal(1, addr.DbNumber); // 西门子 S7-200 V 存储区映射为 DB 1
        Assert.Equal(expectedStart, addr.StartByte);
        Assert.Equal(expectedBit, addr.BitIndex);
        Assert.Equal(expectedIsBit, addr.IsBit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("InvalidAddress")]
    [InlineData("DB.10")]
    [InlineData("DB1.DBX0.8")] // bit > 7
    [InlineData("M0.9")]       // bit > 7
    public void Parse_InvalidAddress_ThrowsException(string raw)
    {
        Assert.ThrowsAny<Exception>(() => S7AddressParser.Parse(raw, TagDataType.Int16));
    }
}
