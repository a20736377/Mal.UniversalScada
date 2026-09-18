namespace Mal.UniversalScada.Drivers.Modbus.Protocol;

/// <summary>
/// Modbus RTU CRC-16 校验计算与校验器
/// 多项式: 0xA001 (Modbus 反转多项式), 初始值: 0xFFFF
/// 采用 256 项查表法实现高性能零内存分配校验
/// </summary>
public static class ModbusCrc
{
    private static readonly ushort[] CrcTable = new ushort[256];

    static ModbusCrc()
    {
        for (ushort i = 0; i < 256; i++)
        {
            ushort crc = i;
            for (int j = 0; j < 8; j++)
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
            CrcTable[i] = crc;
        }
    }

    /// <summary>
    /// 计算指定字节缓冲区的 Modbus CRC-16 校验值
    /// 返回值低 8 位为 CRC 低字节 (Wire First Byte)，高 8 位为 CRC 高字节 (Wire Second Byte)
    /// </summary>
    public static ushort Compute(ReadOnlySpan<byte> buffer)
    {
        ushort crc = 0xFFFF;
        foreach (var b in buffer)
        {
            var tableIndex = (byte)(crc ^ b);
            crc = (ushort)((crc >> 8) ^ CrcTable[tableIndex]);
        }
        return crc;
    }

    /// <summary>
    /// 校验包含末尾 2 字节 CRC 的 Modbus RTU 完整报文是否合法
    /// </summary>
    /// <param name="frameWithCrc">待检验帧，末尾包含低位在前的 2 字节 CRC</param>
    /// <returns>CRC 校验是否一致</returns>
    public static bool Validate(ReadOnlySpan<byte> frameWithCrc)
    {
        if (frameWithCrc.Length < 3) return false;

        var dataSpan = frameWithCrc[..^2];
        var computed = Compute(dataSpan);

        var expectedLow = (byte)(computed & 0xFF);
        var expectedHigh = (byte)((computed >> 8) & 0xFF);

        return frameWithCrc[^2] == expectedLow && frameWithCrc[^1] == expectedHigh;
    }

    /// <summary>
    /// 将计算得到的 CRC 追加到目标缓冲区的末尾 (低字节在前，高字节在后)
    /// </summary>
    public static void AppendCrc(Span<byte> destination, ReadOnlySpan<byte> data)
    {
        var crc = Compute(data);
        destination[data.Length] = (byte)(crc & 0xFF);
        destination[data.Length + 1] = (byte)((crc >> 8) & 0xFF);
    }
}
