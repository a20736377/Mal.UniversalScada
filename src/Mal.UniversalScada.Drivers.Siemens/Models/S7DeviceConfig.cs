using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.Siemens.Enums;

namespace Mal.UniversalScada.Drivers.Siemens.Models;

/// <summary>
/// 西门子设备特异性通信参数配置
/// </summary>
public class S7DeviceConfig
{
    /// <summary>
    /// CPU 硬件系列
    /// </summary>
    public SiemensCpuType CpuType { get; set; } = SiemensCpuType.S71200;

    /// <summary>
    /// 机架号 (Rack，通常为 0)
    /// </summary>
    public int Rack { get; set; } = 0;

    /// <summary>
    /// 槽位号 (Slot，S7-1200/1500 默认为 1；S7-300 默认为 2；S7-400 默认为 2 或 3)
    /// </summary>
    public int Slot { get; set; } = 1;

    /// <summary>
    /// 本地 TSAP (Calling TSAP，默认 0x01 0x00 或由配置指定)
    /// </summary>
    public byte[]? CustomLocalTsap { get; set; }

    /// <summary>
    /// 远程 TSAP (Called TSAP，若未指定则自动根据 Rack/Slot 与 CPU 计算)
    /// </summary>
    public byte[]? CustomRemoteTsap { get; set; }

    /// <summary>
    /// 期望协商的最大 PDU 长度 (西门子默认协商 240 / 480 / 960 字节)
    /// </summary>
    public ushort PreferredPduLength { get; set; } = 960;

    /// <summary>
    /// 实际与 PLC 协商成功后的 PDU 长度
    /// </summary>
    public ushort NegotiatedPduLength { get; set; } = 240;

    /// <summary>
    /// 单个 S7 Read Var 报文中允许携带的最大 S7ANY 变量项数量 (西门子标准协议一般上限 19~20)
    /// </summary>
    public int MaxItemsPerReadJob { get; set; } = 19;

    /// <summary>
    /// 从通用的 DeviceNode 解析生成 S7DeviceConfig
    /// </summary>
    public static S7DeviceConfig FromDeviceNode(DeviceNode device)
    {
        var config = new S7DeviceConfig();

        // 1. 尝试从 CustomProtocolName 字符串解析键值对 (如 "Cpu=S71500;Rack=0;Slot=1;Pdu=960")
        if (!string.IsNullOrWhiteSpace(device.CustomProtocolName))
        {
            var pairs = device.CustomProtocolName.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', StringSplitOptions.TrimEntries);
                if (kv.Length != 2) continue;

                var key = kv[0].ToLowerInvariant();
                var val = kv[1];

                if (key is "cpu" or "cputype")
                {
                    if (Enum.TryParse<SiemensCpuType>(val, true, out var cpu))
                    {
                        config.CpuType = cpu;
                    }
                    else if (val.Contains("200smart", StringComparison.OrdinalIgnoreCase))
                    {
                        config.CpuType = SiemensCpuType.S7200Smart;
                    }
                    else if (val.Contains("200", StringComparison.OrdinalIgnoreCase))
                    {
                        config.CpuType = SiemensCpuType.S7200;
                    }
                    else if (val.Contains("300", StringComparison.OrdinalIgnoreCase))
                    {
                        config.CpuType = SiemensCpuType.S7300;
                    }
                    else if (val.Contains("400", StringComparison.OrdinalIgnoreCase))
                    {
                        config.CpuType = SiemensCpuType.S7400;
                    }
                    else if (val.Contains("1200", StringComparison.OrdinalIgnoreCase))
                    {
                        config.CpuType = SiemensCpuType.S71200;
                    }
                    else if (val.Contains("1500", StringComparison.OrdinalIgnoreCase))
                    {
                        config.CpuType = SiemensCpuType.S71500;
                    }
                }
                else if (key is "rack" && int.TryParse(val, out var r))
                {
                    config.Rack = r;
                }
                else if (key is "slot" && int.TryParse(val, out var s))
                {
                    config.Slot = s;
                }
                else if (key is "pdu" && ushort.TryParse(val, out var p))
                {
                    config.PreferredPduLength = p;
                }
            }
        }
        else
        {
            // 2. 根据 StationAddress 智能判定 Rack 与 Slot
            // 若 StationAddress >= 100，则高位为 Rack，低两位为 Slot (例如 102 -> Rack 1, Slot 2)
            if (device.StationAddress >= 100)
            {
                config.Rack = device.StationAddress / 100;
                config.Slot = device.StationAddress % 100;
            }
            else if (device.StationAddress == 2)
            {
                // 常见 S7-300 默认 Slot 2
                config.Rack = 0;
                config.Slot = 2;
                config.CpuType = SiemensCpuType.S7300;
            }
            else
            {
                config.Rack = 0;
                config.Slot = device.StationAddress > 0 ? device.StationAddress : 1;
            }
        }

        return config;
    }

    /// <summary>
    /// 获取本地 Calling TSAP 字节序列
    /// </summary>
    public byte[] GetLocalTsap()
    {
        if (CustomLocalTsap is { Length: >= 2 }) return CustomLocalTsap;

        return CpuType switch
        {
            SiemensCpuType.S7200Smart => [0x10, 0x00],
            SiemensCpuType.S7200 => [0x10, 0x00],
            _ => [0x01, 0x00] // S7-300/400/1200/1500 常用本地 TSAP
        };
    }

    /// <summary>
    /// 获取远程 Called TSAP 字节序列
    /// </summary>
    public byte[] GetRemoteTsap()
    {
        if (CustomRemoteTsap is { Length: >= 2 }) return CustomRemoteTsap;

        return CpuType switch
        {
            SiemensCpuType.S7200Smart => [0x20, 0x00],
            SiemensCpuType.S7200 => [0x10, 0x01],
            _ => [
                0x02, // 连接类型: 0x01 为 PG, 0x02 为 OP (上位机组态), 0x03 为 Basic
                (byte)((Rack * 0x20) + Slot)
            ]
        };
    }
}
