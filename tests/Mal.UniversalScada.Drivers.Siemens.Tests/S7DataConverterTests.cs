using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.Siemens.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.Siemens.Tests;

public class S7DataConverterTests
{
    [Fact]
    public void DecodeValue_Bool_BitResult_ReturnsCorrectValue()
    {
        var tag = new TagNode { TagId = "T1", DataType = TagDataType.Bool };
        byte[] buffer = [0x01];

        var (val, raw) = S7DataConverter.DecodeValue(buffer, 0, tag, isBitResult: true);

        Assert.Equal(true, val);
        Assert.Equal(true, raw);
    }

    [Fact]
    public void DecodeValue_Bool_BitExtractionFromByte_ReturnsCorrectBit()
    {
        var tag = new TagNode { TagId = "T1", DataType = TagDataType.Bool };
        // 0b0010_0100 = bit 2 is 1, bit 5 is 1
        byte[] buffer = [0b0010_0100];

        var (val2, _) = S7DataConverter.DecodeValue(buffer, 0, tag, isBitResult: false, bitIndex: 2);
        var (val3, _) = S7DataConverter.DecodeValue(buffer, 0, tag, isBitResult: false, bitIndex: 3);
        var (val5, _) = S7DataConverter.DecodeValue(buffer, 0, tag, isBitResult: false, bitIndex: 5);

        Assert.Equal(true, val2);
        Assert.Equal(false, val3);
        Assert.Equal(true, val5);
    }

    [Fact]
    public void DecodeValue_Int16_BigEndian_DecodesCorrectly()
    {
        var tag = new TagNode { TagId = "T1", DataType = TagDataType.Int16 };
        // 0x0100 = 256
        byte[] buffer = [0x01, 0x00];

        var (val, raw) = S7DataConverter.DecodeValue(buffer, 0, tag);

        Assert.Equal((short)256, val);
        Assert.Equal((short)256, raw);
    }

    [Fact]
    public void DecodeValue_Float_BigEndian_DecodesCorrectly()
    {
        var tag = new TagNode { TagId = "T1", DataType = TagDataType.Float };
        // 123.456f in IEEE 754 Big-Endian: 0x42, 0xF6, 0xE9, 0x79
        byte[] buffer = [0x42, 0xF6, 0xE9, 0x79];

        var (val, _) = S7DataConverter.DecodeValue(buffer, 0, tag);

        Assert.IsType<float>(val);
        Assert.InRange((float)val!, 123.455f, 123.457f);
    }

    [Fact]
    public void DecodeValue_WithScaleAndOffset_AppliesLinearConversion()
    {
        // y = 2.0 * x + 10.0
        var tag = new TagNode 
        { 
            TagId = "T1", 
            DataType = TagDataType.Int16,
            ScaleFactor = 2.0,
            Offset = 10.0
        };
        // 0x0005 = 5. y = 2 * 5 + 10 = 20
        byte[] buffer = [0x00, 0x05];

        var (val, raw) = S7DataConverter.DecodeValue(buffer, 0, tag);

        Assert.Equal((short)5, raw);
        Assert.Equal(20.0, Convert.ToDouble(val));
    }

    [Fact]
    public void EncodeValue_Int16_BigEndian_EncodesCorrectly()
    {
        var tag = new TagNode { TagId = "T1", DataType = TagDataType.Int16 };
        short input = 256; // 0x0100

        var bytes = S7DataConverter.EncodeValue(input, tag);

        Assert.Equal(2, bytes.Length);
        Assert.Equal(0x01, bytes[0]);
        Assert.Equal(0x00, bytes[1]);
    }

    [Fact]
    public void EncodeValue_WithScaleAndOffset_AppliesReverseConversion()
    {
        // y = 2.0 * x + 10.0. Input target y = 20 -> x should be (20 - 10) / 2 = 5
        var tag = new TagNode 
        { 
            TagId = "T1", 
            DataType = TagDataType.Int16,
            ScaleFactor = 2.0,
            Offset = 10.0
        };

        var bytes = S7DataConverter.EncodeValue(20.0, tag);

        Assert.Equal(2, bytes.Length);
        Assert.Equal(0x00, bytes[0]);
        Assert.Equal(0x05, bytes[1]);
    }

    [Fact]
    public void EncodeValue_Bool_EncodesCorrectByte()
    {
        var tag = new TagNode { TagId = "T1", DataType = TagDataType.Bool };

        var tBytes = S7DataConverter.EncodeValue(true, tag);
        var fBytes = S7DataConverter.EncodeValue(false, tag);

        Assert.Equal([0x01], tBytes);
        Assert.Equal([0x00], fBytes);
    }
}
