using System.Buffers.Binary;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Drivers.Siemens.Enums;
using Mal.UniversalScada.Drivers.Siemens.Models;
using Mal.UniversalScada.Drivers.Siemens.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.Siemens.Tests;

public class S7MessageCodecTests
{
    [Fact]
    public void CotpHandler_BuildConnectionRequest_ConstructsValidTpktAndCotp()
    {
        byte[] srcTsap = [0x01, 0x00];
        byte[] dstTsap = [0x02, 0x01]; // Rack 0, Slot 1

        var packet = CotpHandler.BuildConnectionRequest(srcTsap, dstTsap);

        // TPKT 校验
        Assert.Equal(0x03, packet[0]); // Version
        Assert.Equal(0x00, packet[1]); // Reserved
        var len = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(2, 2));
        Assert.Equal(packet.Length, len);

        // COTP 校验
        Assert.Equal(0xE0, packet[5]); // CR
        Assert.True(CotpHandler.ValidateConnectionConfirm([0x03, 0x00, 0x00, 0x07, 0x02, 0xD0, 0x00], 7));
    }

    [Fact]
    public void CotpHandler_WrapAndUnwrapDataPdu_PreservesExactPayload()
    {
        byte[] samplePdu = [0x32, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x08, 0x00, 0x00];

        var wrapped = CotpHandler.WrapDataPdu(samplePdu);

        Assert.Equal(0x03, wrapped[0]);
        Assert.Equal(0xF0, wrapped[5]); // DT
        Assert.Equal(wrapped.Length, 4 + 3 + samplePdu.Length);

        var unwrapped = CotpHandler.UnwrapDataPdu(wrapped, wrapped.Length);
        Assert.Equal(samplePdu, unwrapped.ToArray());
    }

    [Fact]
    public void S7MessageCodec_SetupCommunication_EncodeAndDecode()
    {
        var pdu = S7MessageCodec.BuildSetupCommunicationPdu(10, 960);

        Assert.Equal(0x32, pdu[0]); // S7 Protocol
        Assert.Equal(0x01, pdu[1]); // ROSCTR Job
        Assert.Equal(0xF0, pdu[10]); // Func Setup

        // 构造模拟应答 Ack-Data
        var ackPdu = new byte[12 + 8];
        ackPdu[0] = 0x32;
        ackPdu[1] = 0x03; // Ack-Data
        BinaryPrimitives.WriteUInt16BigEndian(ackPdu.AsSpan(4, 2), 10);
        BinaryPrimitives.WriteUInt16BigEndian(ackPdu.AsSpan(6, 2), 8); // param len
        BinaryPrimitives.WriteUInt16BigEndian(ackPdu.AsSpan(8, 2), 0); // data len
        ackPdu[10] = 0x00; // ErrorClass
        ackPdu[11] = 0x00; // ErrorCode
        ackPdu[12] = 0xF0; // Func
        BinaryPrimitives.WriteUInt16BigEndian(ackPdu.AsSpan(18, 2), 480); // Negotiated Pdu = 480

        var negotiated = S7MessageCodec.ParseSetupCommunicationAck(ackPdu);
        Assert.Equal((ushort)480, negotiated);
    }

    [Fact]
    public void S7MessageCodec_BuildReadVarPdu_GeneratesCorrectStructure()
    {
        var addr1 = S7AddressParser.Parse("DB1.DBD0", TagDataType.Float);
        var addr2 = S7AddressParser.Parse("M0.0", TagDataType.Bool);

        var pdu = S7MessageCodec.BuildReadVarPdu(1, [addr1, addr2]);

        Assert.Equal(0x32, pdu[0]);
        Assert.Equal(0x01, pdu[1]);
        Assert.Equal(0x04, pdu[10]); // Func Read Var
        Assert.Equal(2, pdu[11]);    // Item count = 2
    }

    [Fact]
    public void S7MessageCodec_ParseReadVarAck_ExtractsItemsCorrectly()
    {
        // 模拟 2 个点位的应答:
        // Item 1: 0xFF (Success), Transport: Byte (0x04), Length: 32 bits (4 bytes), Data: [0x42, 0xF6, 0xE9, 0x79] (Float 123.456)
        // Item 2: 0xFF (Success), Transport: Bit (0x03), Length: 1 bit, Data: [0x01] (True)
        var pdu = new byte[12 + 2 + (4 + 4) + (4 + 1)];
        pdu[0] = 0x32;
        pdu[1] = 0x03; // Ack-Data
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(6, 2), 2); // param len
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(8, 2), (ushort)(pdu.Length - 14)); // data len
        pdu[10] = 0x00; // err class
        pdu[11] = 0x00; // err code
        pdu[12] = 0x04; // Func Read Var
        pdu[13] = 2;    // Item count

        int cur = 14;
        // Item 1
        pdu[cur++] = 0xFF; // Return code Success
        pdu[cur++] = 0x04; // Transport ByteWordDword
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(cur, 2), 32); // 32 bits
        cur += 2;
        pdu[cur++] = 0x42;
        pdu[cur++] = 0xF6;
        pdu[cur++] = 0xE9;
        pdu[cur++] = 0x79;

        // Item 2
        pdu[cur++] = 0xFF; // Return code Success
        pdu[cur++] = 0x03; // Transport Bit
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(cur, 2), 1); // 1 bit
        cur += 2;
        pdu[cur++] = 0x01; // True

        var items = S7MessageCodec.ParseReadVarAck(pdu, 2);

        Assert.Equal(2, items.Count);
        Assert.True(items[0].IsSuccess);
        Assert.Equal(4, items[0].Data.Length);
        Assert.False(items[0].IsBit);

        Assert.True(items[1].IsSuccess);
        Assert.Equal([0x01], items[1].Data);
        Assert.True(items[1].IsBit);
    }
}
