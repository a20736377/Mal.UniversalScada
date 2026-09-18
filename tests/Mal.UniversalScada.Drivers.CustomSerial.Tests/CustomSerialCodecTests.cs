using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.CustomSerial.Enums;
using Mal.UniversalScada.Drivers.CustomSerial.Models;
using Mal.UniversalScada.Drivers.CustomSerial.Protocol;
using Xunit;

namespace Mal.UniversalScada.Drivers.CustomSerial.Tests;

public class CustomSerialCodecTests
{
    [Fact]
    public void BuildAndUnpack_BinaryFrame_Roundtrip_Success()
    {
        var config = new CustomSerialConfig
        {
            Header = [0xAA, 0x55],
            Tail = [0x0D, 0x0A],
            CheckType = CustomSerialCheckType.Sum8,
            StationAddress = 1
        };

        byte cmd = 0x03;
        byte[] payload = [0x12, 0x34, 0x56, 0x78];

        var frame = CustomSerialCodec.BuildBinaryRequest(config, cmd, payload);

        // 验证帧头
        Assert.Equal(0xAA, frame[0]);
        Assert.Equal(0x55, frame[1]);
        // 验证帧尾
        Assert.Equal(0x0D, frame[^2]);
        Assert.Equal(0x0A, frame[^1]);

        var success = CustomSerialCodec.TryUnpackBinaryResponse(frame, config, out var outCmd, out var outPayload, out var err);
        Assert.True(success, err);
        Assert.Equal(cmd, outCmd);
        Assert.Equal(payload, outPayload);
    }

    [Fact]
    public void DecodePayload_Int16AndScaling_CalculatesProperly()
    {
        var addr = new CustomSerialAddress { ByteOffset = 2, DataType = TagDataType.Int16 };
        var tag = new TagNode
        {
            TagId = "T1",
            DataType = TagDataType.Int16,
            ScaleFactor = 0.5,
            Offset = 5.0
        };

        // 有效载荷: [xx, xx, 0x00, 0x64] (偏移 2 处为 100)
        byte[] payload = [0x00, 0x00, 0x00, 0x64];

        var (val, raw) = CustomSerialCodec.DecodePayload(payload, addr, tag, isBigEndian: true);

        Assert.Equal((short)100, raw);
        // y = 100 * 0.5 + 5.0 = 55.0
        Assert.Equal(55.0, (double)val!, 3);
    }

    [Fact]
    public void DecodeAsciiLine_CsvIndex_ExtractsValue()
    {
        var addr = new CustomSerialAddress { FieldIndex = 1, DataType = TagDataType.Float };
        var tag = new TagNode { TagId = "T_Ascii", DataType = TagDataType.Float };

        string line = "DATA,123.45,67.89\r\n";
        var (val, raw) = CustomSerialCodec.DecodeAsciiLine(line, addr, tag);

        Assert.Equal(123.45, Convert.ToDouble(val), 2);
    }
}
