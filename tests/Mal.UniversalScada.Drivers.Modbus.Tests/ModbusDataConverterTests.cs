using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.Modbus.Enums;
using Mal.UniversalScada.Drivers.Modbus.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.Modbus.Tests;

public class ModbusDataConverterTests
{
    [Fact]
    public void ReadAndWriteInt16_Roundtrip_Matches()
    {
        short original = -12345;
        var bytes = ModbusDataConverter.WriteInt16(original, ModbusEndian.ABCD);
        var read = ModbusDataConverter.ReadInt16(bytes, ModbusEndian.ABCD);
        Assert.Equal(original, read);

        // Byte-swap mode (BADC)
        var bytesBadc = ModbusDataConverter.WriteInt16(original, ModbusEndian.BADC);
        var readBadc = ModbusDataConverter.ReadInt16(bytesBadc, ModbusEndian.BADC);
        Assert.Equal(original, readBadc);
    }

    [Theory]
    [InlineData(ModbusEndian.ABCD)]
    [InlineData(ModbusEndian.CDAB)]
    [InlineData(ModbusEndian.BADC)]
    [InlineData(ModbusEndian.DCBA)]
    public void ReadAndWriteFloat_AllEndianModes_Matches(ModbusEndian endian)
    {
        float original = 123.456f;
        var bytes = ModbusDataConverter.WriteFloat(original, endian);
        var read = ModbusDataConverter.ReadFloat(bytes, endian);

        Assert.Equal(original, read, precision: 3);
    }

    [Theory]
    [InlineData(ModbusEndian.ABCD)]
    [InlineData(ModbusEndian.CDAB)]
    [InlineData(ModbusEndian.BADC)]
    [InlineData(ModbusEndian.DCBA)]
    public void ReadAndWriteInt32_AllEndianModes_Matches(ModbusEndian endian)
    {
        int original = -987654321;
        var bytes = ModbusDataConverter.WriteInt32(original, endian);
        var read = ModbusDataConverter.ReadInt32(bytes, endian);

        Assert.Equal(original, read);
    }

    [Theory]
    [InlineData(ModbusEndian.ABCD)]
    [InlineData(ModbusEndian.CDAB)]
    [InlineData(ModbusEndian.BADC)]
    [InlineData(ModbusEndian.DCBA)]
    public void ReadAndWriteDouble_AllEndianModes_Matches(ModbusEndian endian)
    {
        double original = 3.141592653589793;
        var bytes = ModbusDataConverter.WriteDouble(original, endian);
        var read = ModbusDataConverter.ReadDouble(bytes, endian);

        Assert.Equal(original, read, precision: 6);
    }

    [Fact]
    public void DecodeValue_WithScalingAndOffset_CalculatesCorrectly()
    {
        var tag = new TagNode
        {
            Id = 1,
            DataType = TagDataType.Int16,
            ScaleFactor = 0.1,
            Offset = 10.0
        };

        // 原始值 100
        byte[] rawBytes = [0x00, 0x64];
        var (val, raw) = ModbusDataConverter.DecodeValue(rawBytes, tag, ModbusEndian.ABCD);

        Assert.Equal((short)100, raw);
        // y = 100 * 0.1 + 10.0 = 20.0 (按浮点工程值返回)
        Assert.Equal(20.0, Convert.ToDouble(val), 3);
    }

    [Fact]
    public void DecodeValue_BitInRegister_ReturnsCorrectBoolean()
    {
        var tag = new TagNode
        {
            Id = 2,
            DataType = TagDataType.Bool
        };

        // 寄存器值 0x0020 (二进制第 5 位置 1)
        byte[] rawBytes = [0x00, 0x20];

        var (val5, _) = ModbusDataConverter.DecodeValue(rawBytes, tag, ModbusEndian.ABCD, bitIndex: 5);
        Assert.Equal(true, val5);

        var (val4, _) = ModbusDataConverter.DecodeValue(rawBytes, tag, ModbusEndian.ABCD, bitIndex: 4);
        Assert.Equal(false, val4);
    }

    [Fact]
    public void EncodeValue_WithScalingAndOffset_PerformsInverseScaling()
    {
        var tag = new TagNode
        {
            Id = 1,
            DataType = TagDataType.Int16,
            ScaleFactor = 0.1,
            Offset = 10.0
        };

        // 设定工程值 20.0 -> 原始值应当为 (20.0 - 10.0) / 0.1 = 100 (0x0064)
        var bytes = ModbusDataConverter.EncodeValue(20.0, tag, ModbusEndian.ABCD);
        var rawVal = ModbusDataConverter.ReadInt16(bytes, ModbusEndian.ABCD);

        Assert.Equal(100, rawVal);
    }
}
