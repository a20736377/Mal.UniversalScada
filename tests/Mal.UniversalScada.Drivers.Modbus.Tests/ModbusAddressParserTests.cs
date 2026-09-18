using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Drivers.Modbus.Enums;
using Mal.UniversalScada.Drivers.Modbus.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.Modbus.Tests;

public class ModbusAddressParserTests
{
    [Theory]
    [InlineData("40001", ModbusRegisterType.HoldingRegister, 0)]
    [InlineData("40010", ModbusRegisterType.HoldingRegister, 9)]
    [InlineData("30001", ModbusRegisterType.InputRegister, 0)]
    [InlineData("30005", ModbusRegisterType.InputRegister, 4)]
    [InlineData("10001", ModbusRegisterType.DiscreteInput, 0)]
    [InlineData("10020", ModbusRegisterType.DiscreteInput, 19)]
    [InlineData("00001", ModbusRegisterType.Coil, 0)]
    [InlineData("00008", ModbusRegisterType.Coil, 7)]
    public void Parse_Classic5Digit_ReturnsExpectedTypeAndOffset(string raw, ModbusRegisterType expectedType, ushort expectedStart)
    {
        var addr = ModbusAddressParser.Parse(raw, TagDataType.Int16);
        Assert.Equal(expectedType, addr.RegisterType);
        Assert.Equal(expectedStart, addr.StartAddress);
        Assert.False(addr.IsBitInRegister);
    }

    [Theory]
    [InlineData("400001", ModbusRegisterType.HoldingRegister, 0)]
    [InlineData("400010", ModbusRegisterType.HoldingRegister, 9)]
    [InlineData("300001", ModbusRegisterType.InputRegister, 0)]
    [InlineData("100001", ModbusRegisterType.DiscreteInput, 0)]
    [InlineData("000001", ModbusRegisterType.Coil, 0)]
    public void Parse_Classic6Digit_ReturnsExpectedTypeAndOffset(string raw, ModbusRegisterType expectedType, ushort expectedStart)
    {
        var addr = ModbusAddressParser.Parse(raw, TagDataType.Int16);
        Assert.Equal(expectedType, addr.RegisterType);
        Assert.Equal(expectedStart, addr.StartAddress);
    }

    [Theory]
    [InlineData("HR0", ModbusRegisterType.HoldingRegister, 0)]
    [InlineData("HR1", ModbusRegisterType.HoldingRegister, 0)]
    [InlineData("HR2", ModbusRegisterType.HoldingRegister, 1)]
    [InlineData("IR0", ModbusRegisterType.InputRegister, 0)]
    [InlineData("IR1", ModbusRegisterType.InputRegister, 0)]
    [InlineData("C0", ModbusRegisterType.Coil, 0)]
    [InlineData("C1", ModbusRegisterType.Coil, 0)]
    [InlineData("DI0", ModbusRegisterType.DiscreteInput, 0)]
    [InlineData("DI1", ModbusRegisterType.DiscreteInput, 0)]
    [InlineData("4x0001", ModbusRegisterType.HoldingRegister, 0)]
    [InlineData("3x0001", ModbusRegisterType.InputRegister, 0)]
    [InlineData("1x0001", ModbusRegisterType.DiscreteInput, 0)]
    [InlineData("0x0001", ModbusRegisterType.Coil, 0)]
    public void Parse_PrefixNotations_ReturnsExpected(string raw, ModbusRegisterType expectedType, ushort expectedStart)
    {
        var addr = ModbusAddressParser.Parse(raw, TagDataType.Int16);
        Assert.Equal(expectedType, addr.RegisterType);
        Assert.Equal(expectedStart, addr.StartAddress);
    }

    [Fact]
    public void Parse_BitInRegister_ReturnsBitIndex()
    {
        var addr = ModbusAddressParser.Parse("40001.0", TagDataType.Bool);
        Assert.Equal(ModbusRegisterType.HoldingRegister, addr.RegisterType);
        Assert.Equal(0, addr.StartAddress);
        Assert.True(addr.IsBitInRegister);
        Assert.Equal(0, addr.BitIndex);
        Assert.Equal(1, addr.RegisterCount);

        var addr15 = ModbusAddressParser.Parse("40001.15", TagDataType.Bool);
        Assert.Equal(15, addr15.BitIndex);

        var hrBit = ModbusAddressParser.Parse("HR10.5", TagDataType.Bool);
        Assert.Equal(ModbusRegisterType.HoldingRegister, hrBit.RegisterType);
        Assert.Equal(9, hrBit.StartAddress);
        Assert.Equal(5, hrBit.BitIndex);
    }

    [Fact]
    public void Parse_DataTypes_CalculatesCorrectRegisterCounts()
    {
        Assert.Equal(1, ModbusAddressParser.Parse("40001", TagDataType.Int16).RegisterCount);
        Assert.Equal(1, ModbusAddressParser.Parse("40001", TagDataType.UInt16).RegisterCount);
        Assert.Equal(2, ModbusAddressParser.Parse("40001", TagDataType.Int32).RegisterCount);
        Assert.Equal(2, ModbusAddressParser.Parse("40001", TagDataType.UInt32).RegisterCount);
        Assert.Equal(2, ModbusAddressParser.Parse("40001", TagDataType.Float).RegisterCount);
        Assert.Equal(4, ModbusAddressParser.Parse("40001", TagDataType.Int64).RegisterCount);
        Assert.Equal(4, ModbusAddressParser.Parse("40001", TagDataType.Double).RegisterCount);
        Assert.Equal(1, ModbusAddressParser.Parse("00001", TagDataType.Bool).RegisterCount);
    }

    [Fact]
    public void Parse_ZeroBased_SubtractsProperly()
    {
        // zeroBased 为 true 时，40000 对应 0，40001 对应 1
        var addr0 = ModbusAddressParser.Parse("40000", TagDataType.Int16, zeroBased: true);
        Assert.Equal(0, addr0.StartAddress);

        var addr1 = ModbusAddressParser.Parse("40001", TagDataType.Int16, zeroBased: true);
        Assert.Equal(1, addr1.StartAddress);
    }
}
