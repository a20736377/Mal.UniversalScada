using System.Buffers.Binary;
using Mal.UniversalScada.Drivers.Modbus.Enums;
using Mal.UniversalScada.Drivers.Modbus.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.Modbus.Tests;

public class ModbusPduCodecTests
{
    [Fact]
    public void BuildReadRequestPdu_HoldingRegisters_ProducesCorrectPdu()
    {
        // 读保持寄存器起始 100, 数量 10 -> [0x03, 0x00, 0x64, 0x00, 0x0A]
        var pdu = ModbusPduCodec.BuildReadRequestPdu(ModbusRegisterType.HoldingRegister, 100, 10);

        Assert.Equal(5, pdu.Length);
        Assert.Equal(0x03, pdu[0]);
        Assert.Equal(100, BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2)));
        Assert.Equal(10, BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2)));
    }

    [Fact]
    public void BuildWriteSingleCoilPdu_TrueAndFalse_ProducesFf00And0000()
    {
        var pduOn = ModbusPduCodec.BuildWriteSingleCoilPdu(5, true);
        Assert.Equal(0x05, pduOn[0]);
        Assert.Equal(5, BinaryPrimitives.ReadUInt16BigEndian(pduOn.AsSpan(1, 2)));
        Assert.Equal((ushort)0xFF00, BinaryPrimitives.ReadUInt16BigEndian(pduOn.AsSpan(3, 2)));

        var pduOff = ModbusPduCodec.BuildWriteSingleCoilPdu(5, false);
        Assert.Equal((ushort)0x0000, BinaryPrimitives.ReadUInt16BigEndian(pduOff.AsSpan(3, 2)));
    }

    [Fact]
    public void BuildWriteMultipleRegistersPdu_TwoWords_ProducesCorrectPdu()
    {
        byte[] regBytes = [0x12, 0x34, 0x56, 0x78];
        var pdu = ModbusPduCodec.BuildWriteMultipleRegistersPdu(20, regBytes);

        // [0x10, 0x00, 0x14, 0x00, 0x02, 0x04, 0x12, 0x34, 0x56, 0x78]
        Assert.Equal(10, pdu.Length);
        Assert.Equal(0x10, pdu[0]);
        Assert.Equal(20, BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2)));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2)));
        Assert.Equal(4, pdu[5]);
        Assert.Equal(regBytes, pdu[6..]);
    }

    [Fact]
    public void ParseReadResponse_ValidData_ReturnsDataSpan()
    {
        // 应答 FC03: [0x03, 0x04, 0x00, 0x01, 0x00, 0x02] (2 个寄存器)
        byte[] respPdu = [0x03, 0x04, 0x00, 0x01, 0x00, 0x02];
        var data = ModbusPduCodec.ParseReadResponse(respPdu, ModbusPduCodec.FcReadHoldingRegisters);

        Assert.Equal(4, data.Length);
        Assert.Equal([0x00, 0x01, 0x00, 0x02], data.ToArray());
    }

    [Fact]
    public void ParseReadResponse_ExceptionResponse_ThrowsModbusException()
    {
        // 异常应答: [0x83, 0x02] (Illegal Data Address)
        byte[] exceptionPdu = [0x83, 0x02];

        var ex = Assert.Throws<ModbusException>(() =>
        {
            ModbusPduCodec.ParseReadResponse(exceptionPdu, ModbusPduCodec.FcReadHoldingRegisters);
        });

        Assert.Equal(0x02, ex.ExceptionCode);
        Assert.Contains("非法数据地址", ex.Message);
    }
}
