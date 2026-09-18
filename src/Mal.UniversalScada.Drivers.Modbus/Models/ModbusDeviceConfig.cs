using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.Modbus.Enums;

namespace Mal.UniversalScada.Drivers.Modbus.Models;

/// <summary>
/// Modbus 设备特异性通信参数配置
/// </summary>
public class ModbusDeviceConfig
{
    /// <summary>
    /// 从站地址 / 单元标识符 (Slave ID / Unit ID，范围 1 ~ 247，广播为 0)
    /// </summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>
    /// 多字节与多字排列字序 (ABCD, CDAB, BADC, DCBA)
    /// </summary>
    public ModbusEndian Endian { get; set; } = ModbusEndian.ABCD;

    /// <summary>
    /// 单次读取保持寄存器/输入寄存器的最大字数上限 (Modbus 规范协议上限 125 字，默认 120 字)
    /// </summary>
    public int MaxRegistersPerRead { get; set; } = 120;

    /// <summary>
    /// 单次读取线圈/离散输入的最大位数量 (Modbus 规范上限 2000 位，默认 1920 位)
    /// </summary>
    public int MaxCoilsPerRead { get; set; } = 1920;

    /// <summary>
    /// 连续地址合并打包时允许容忍的最大空洞寄存器数量 (默认 5，在网络/串口延时大时有效减少往返次数)
    /// </summary>
    public int MaxRegisterGap { get; set; } = 5;

    /// <summary>
    /// 组态地址是否直接按 0 偏移基准 (默认 false，即 40001 对应寄存器偏移 0；若为 true 则 40000 对应 0，40001 对应 1)
    /// </summary>
    public bool ZeroBased { get; set; } = false;

    /// <summary>
    /// 从通用的 DeviceNode 解析生成 ModbusDeviceConfig
    /// </summary>
    public static ModbusDeviceConfig FromDeviceNode(DeviceNode device)
    {
        var config = new ModbusDeviceConfig
        {
            SlaveId = device.StationAddress > 0 && device.StationAddress <= 255 
                ? (byte)device.StationAddress 
                : (byte)1
        };

        // 尝试从 CustomProtocolName 解析扩展参数，如 "Endian=CDAB;MaxRegisters=100;Gap=2"
        if (!string.IsNullOrWhiteSpace(device.CustomProtocolName))
        {
            var pairs = device.CustomProtocolName.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', StringSplitOptions.TrimEntries);
                if (kv.Length != 2) continue;

                var key = kv[0].ToLowerInvariant();
                var val = kv[1];

                if (key is "endian" or "byteorder")
                {
                    if (Enum.TryParse<ModbusEndian>(val, true, out var endian))
                    {
                        config.Endian = endian;
                    }
                }
                else if (key is "wordswap" or "swap")
                {
                    if (bool.TryParse(val, out var swap) && swap)
                    {
                        config.Endian = ModbusEndian.CDAB;
                    }
                }
                else if (key is "maxregisters" or "maxwords")
                {
                    if (int.TryParse(val, out var maxR))
                    {
                        config.MaxRegistersPerRead = Math.Clamp(maxR, 1, 125);
                    }
                }
                else if (key is "maxcoils" or "maxbits")
                {
                    if (int.TryParse(val, out var maxC))
                    {
                        config.MaxCoilsPerRead = Math.Clamp(maxC, 1, 2000);
                    }
                }
                else if (key is "gap" or "maxgap")
                {
                    if (int.TryParse(val, out var gap))
                    {
                        config.MaxRegisterGap = Math.Max(0, gap);
                    }
                }
                else if (key is "zerobased")
                {
                    if (bool.TryParse(val, out var zb))
                    {
                        config.ZeroBased = zb;
                    }
                }
                else if (key is "slaveid" or "station" or "unitid")
                {
                    if (byte.TryParse(val, out var sId))
                    {
                        config.SlaveId = sId;
                    }
                }
            }
        }

        return config;
    }
}
