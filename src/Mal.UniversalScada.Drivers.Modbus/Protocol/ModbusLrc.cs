using System.Text;

namespace Mal.UniversalScada.Drivers.Modbus.Protocol;

/// <summary>
/// Modbus ASCII LRC (纵向冗余校验) 计算与 ASCII 帧编解码器
/// </summary>
public static class ModbusLrc
{
    /// <summary>
    /// 计算指定二进制缓冲区的 LRC 校验码
    /// 算法: 所有字节无符号累加和的补码 ((0xFF - (sum & 0xFF) + 1) & 0xFF)
    /// </summary>
    public static byte Compute(ReadOnlySpan<byte> buffer)
    {
        byte sum = 0;
        foreach (var b in buffer)
        {
            sum += b;
        }
        return (byte)((-sum) & 0xFF);
    }

    /// <summary>
    /// 将从站号与 PDU 封装为完整的 Modbus ASCII 传输帧
    /// 格式: ':' + Hex(SlaveId + Pdu + LRC) + "\r\n"
    /// </summary>
    public static byte[] EncodeAsciiFrame(byte slaveId, ReadOnlySpan<byte> pdu)
    {
        var binLen = 1 + pdu.Length;
        Span<byte> binary = stackalloc byte[binLen];
        binary[0] = slaveId;
        pdu.CopyTo(binary[1..]);

        var lrc = Compute(binary);

        // 输出长度: 1(冒号) + (binLen + 1) * 2(Hex字符) + 2(\r\n)
        var hexCharCount = (binLen + 1) * 2;
        var totalLen = 1 + hexCharCount + 2;
        var asciiBytes = new byte[totalLen];

        asciiBytes[0] = (byte)':';

        // 编码 slaveId
        EncodeByteToHex(slaveId, asciiBytes.AsSpan(1, 2));

        // 编码 pdu
        for (int i = 0; i < pdu.Length; i++)
        {
            EncodeByteToHex(pdu[i], asciiBytes.AsSpan(3 + i * 2, 2));
        }

        // 编码 LRC
        EncodeByteToHex(lrc, asciiBytes.AsSpan(1 + binLen * 2, 2));

        // 编码 CRLF
        asciiBytes[^2] = (byte)'\r';
        asciiBytes[^1] = (byte)'\n';

        return asciiBytes;
    }

    /// <summary>
    /// 解析收到的 Modbus ASCII 帧并校验 LRC
    /// </summary>
    public static bool TryDecodeAsciiFrame(
        ReadOnlySpan<byte> asciiFrame, 
        out byte slaveId, 
        out byte[] pdu, 
        out string? error)
    {
        slaveId = 0;
        pdu = [];
        error = null;

        // 去除可能的前导和后导空白/CRLF
        ReadOnlySpan<byte> trimBytes = stackalloc byte[] { (byte)' ', (byte)'\r', (byte)'\n', (byte)'\t' };
        var frame = asciiFrame.Trim(trimBytes);
        if (frame.Length == 0 || frame[0] != (byte)':')
        {
            error = "ASCII 帧缺少起始字符 ':'";
            return false;
        }

        var hexSpan = frame[1..];
        if (hexSpan.Length % 2 != 0 || hexSpan.Length < 4) // 至少 Slave(2) + LRC(2)
        {
            error = "ASCII 帧 HEX 长度非法或不足";
            return false;
        }

        var binLen = hexSpan.Length / 2;
        var binary = new byte[binLen];
        for (int i = 0; i < binLen; i++)
        {
            if (!TryHexToByte(hexSpan.Slice(i * 2, 2), out var b))
            {
                error = $"ASCII 帧包含非法的非十六进制字符 (位置 {i * 2})";
                return false;
            }
            binary[i] = b;
        }

        // 校验 LRC
        var dataWithoutLrc = binary.AsSpan(0, binLen - 1);
        var expectedLrc = binary[^1];
        var actualLrc = Compute(dataWithoutLrc);

        if (expectedLrc != actualLrc)
        {
            error = $"LRC 校验失败: 期望 0x{expectedLrc:X2}, 实际 0x{actualLrc:X2}";
            return false;
        }

        slaveId = binary[0];
        pdu = binary[1..^1];
        return true;
    }

    private static void EncodeByteToHex(byte b, Span<byte> dest)
    {
        const string hexChars = "0123456789ABCDEF";
        dest[0] = (byte)hexChars[(b >> 4) & 0x0F];
        dest[1] = (byte)hexChars[b & 0x0F];
    }

    private static bool TryHexToByte(ReadOnlySpan<byte> hex, out byte result)
    {
        result = 0;
        int hi = HexVal(hex[0]);
        int lo = HexVal(hex[1]);
        if (hi < 0 || lo < 0) return false;
        result = (byte)((hi << 4) | lo);
        return true;
    }

    private static int HexVal(byte c)
    {
        if (c is >= (byte)'0' and <= (byte)'9') return c - (byte)'0';
        if (c is >= (byte)'A' and <= (byte)'F') return c - (byte)'A' + 10;
        if (c is >= (byte)'a' and <= (byte)'f') return c - (byte)'a' + 10;
        return -1;
    }
}
