using System.Globalization;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.CustomSerial.Enums;

namespace Mal.UniversalScada.Drivers.CustomSerial.Models;

/// <summary>
/// 串口私有协议配置模型
/// </summary>
public class CustomSerialConfig
{
    /// <summary>
    /// 报文封装模式
    /// </summary>
    public CustomSerialFrameMode FrameMode { get; set; } = CustomSerialFrameMode.BinaryHeaderTail;

    /// <summary>
    /// 帧头字节序列 (默认 0xAA 0x55)
    /// </summary>
    public byte[] Header { get; set; } = [0xAA, 0x55];

    /// <summary>
    /// 帧尾字节序列 (默认 0x0D 0x0A，可为空)
    /// </summary>
    public byte[] Tail { get; set; } = [0x0D, 0x0A];

    /// <summary>
    /// 校验类型 (默认 Sum8)
    /// </summary>
    public CustomSerialCheckType CheckType { get; set; } = CustomSerialCheckType.Sum8;

    /// <summary>
    /// 设备站号 (默认 1)
    /// </summary>
    public byte StationAddress { get; set; } = 1;

    /// <summary>
    /// 固定报文字节长度 (仅在 FixedLength 模式下生效)
    /// </summary>
    public int FixedFrameSize { get; set; } = 8;

    /// <summary>
    /// 是否为大端字节序 (默认 true)
    /// </summary>
    public bool IsBigEndian { get; set; } = true;

    /// <summary>
    /// 单次查询超时时间 (毫秒)
    /// </summary>
    public int TimeoutMs { get; set; } = 1500;

    /// <summary>
    /// 从 DeviceNode 解析生成配置
    /// </summary>
    public static CustomSerialConfig FromDeviceNode(DeviceNode device)
    {
        var config = new CustomSerialConfig
        {
            StationAddress = (byte)(device.StationAddress > 0 ? device.StationAddress : 1),
            TimeoutMs = device.TimeoutMs > 0 ? device.TimeoutMs : 1500
        };

        if (string.IsNullOrWhiteSpace(device.CustomProtocolName))
        {
            return config;
        }

        var pairs = device.CustomProtocolName.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var kv = pair.Split('=', StringSplitOptions.TrimEntries);
            if (kv.Length != 2) continue;

            var key = kv[0].ToLowerInvariant();
            var val = kv[1];

            if (key is "mode" or "framemode")
            {
                if (Enum.TryParse<CustomSerialFrameMode>(val, true, out var mode))
                {
                    config.FrameMode = mode;
                }
                else if (val.Contains("ascii", StringComparison.OrdinalIgnoreCase))
                {
                    config.FrameMode = CustomSerialFrameMode.AsciiLine;
                }
            }
            else if (key is "header" or "head")
            {
                config.Header = ParseHexBytes(val);
            }
            else if (key is "tail")
            {
                config.Tail = ParseHexBytes(val);
            }
            else if (key is "check" or "checksum")
            {
                if (Enum.TryParse<CustomSerialCheckType>(val, true, out var chk))
                {
                    config.CheckType = chk;
                }
                else if (val.Contains("xor", StringComparison.OrdinalIgnoreCase) || val.Contains("bcc", StringComparison.OrdinalIgnoreCase))
                {
                    config.CheckType = CustomSerialCheckType.Xor8;
                }
                else if (val.Contains("crc", StringComparison.OrdinalIgnoreCase))
                {
                    config.CheckType = CustomSerialCheckType.Crc16Modbus;
                }
                else if (val.Contains("none", StringComparison.OrdinalIgnoreCase))
                {
                    config.CheckType = CustomSerialCheckType.None;
                }
            }
            else if (key is "endian")
            {
                config.IsBigEndian = !val.Contains("little", StringComparison.OrdinalIgnoreCase);
            }
            else if (key is "size" or "length" && int.TryParse(val, out var size))
            {
                config.FixedFrameSize = size;
            }
        }

        return config;
    }

    private static byte[] ParseHexBytes(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return [];
        var clean = hex.Replace(" ", "").Replace("0x", "").Replace("0X", "").Replace("-", "");
        if (clean.Length % 2 != 0) clean = "0" + clean;

        var bytes = new byte[clean.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(clean.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        return bytes;
    }
}
