using Mal.UniversalScada.Drivers.CustomSerial.Enums;

namespace Mal.UniversalScada.Drivers.CustomSerial.Protocol;

/// <summary>
/// 串口通信常用校验算法工具类 (Sum8, Xor8/BCC, CRC16)
/// </summary>
public static class CustomSerialChecksum
{
    public static byte ComputeSum8(ReadOnlySpan<byte> span)
    {
        byte sum = 0;
        foreach (var b in span)
        {
            sum += b;
        }
        return sum;
    }

    public static byte ComputeXor8(ReadOnlySpan<byte> span)
    {
        byte xor = 0;
        foreach (var b in span)
        {
            xor ^= b;
        }
        return xor;
    }

    public static ushort ComputeCrc16Modbus(ReadOnlySpan<byte> span)
    {
        ushort crc = 0xFFFF;
        foreach (var b in span)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 1) == 1)
                {
                    crc = (ushort)((crc >> 1) ^ 0xA001);
                }
                else
                {
                    crc >>= 1;
                }
            }
        }
        return crc;
    }

    public static byte[] Compute(CustomSerialCheckType type, ReadOnlySpan<byte> span)
    {
        switch (type)
        {
            case CustomSerialCheckType.Sum8:
                return [ComputeSum8(span)];
            case CustomSerialCheckType.Xor8:
                return [ComputeXor8(span)];
            case CustomSerialCheckType.Crc16Modbus:
            {
                var crc = ComputeCrc16Modbus(span);
                return [(byte)(crc & 0xFF), (byte)((crc >> 8) & 0xFF)]; // 低位在前
            }
            case CustomSerialCheckType.Crc16Ccitt:
            {
                var crc = ComputeCrc16Modbus(span);
                return [(byte)((crc >> 8) & 0xFF), (byte)(crc & 0xFF)]; // 高位在前
            }
            default:
                return [];
        }
    }

    public static bool Validate(CustomSerialCheckType type, ReadOnlySpan<byte> dataSpan, ReadOnlySpan<byte> checkSpan)
    {
        if (type == CustomSerialCheckType.None) return true;

        var expected = Compute(type, dataSpan);
        if (checkSpan.Length < expected.Length) return false;

        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != checkSpan[i]) return false;
        }
        return true;
    }
}
