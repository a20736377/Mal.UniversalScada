using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Drivers;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Core.Services;
using Xunit;

namespace Mal.UniversalScada.Core.Tests;

public class Stage3CommercialTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connString;

    public Stage3CommercialTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"scada_test_{Guid.NewGuid():N}.db");
        _connString = $"Data Source={_testDbPath}";
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch { }
    }

    #region 1. 三菱 MC 协议编解码与地址解析测试

    [Fact]
    public void MitsubishiMcDriver_AddressParser_ShouldParseRegistersCorrectly()
    {
        // 1. D 区字寄存器
        Assert.True(MitsubishiMcDriver.TryParseAddress("D100", out var dAddr));
        Assert.Equal("D", dAddr.Area);
        Assert.Equal(0xA8, dAddr.Code);
        Assert.Equal(100, dAddr.Address);
        Assert.False(dAddr.IsBit);

        // 2. M 区位寄存器
        Assert.True(MitsubishiMcDriver.TryParseAddress("M255", out var mAddr));
        Assert.Equal("M", mAddr.Area);
        Assert.Equal(0x90, mAddr.Code);
        Assert.Equal(255, mAddr.Address);
        Assert.True(mAddr.IsBit);

        // 3. W 区 16 进制字寄存器
        Assert.True(MitsubishiMcDriver.TryParseAddress("W1A", out var wAddr));
        Assert.Equal("W", wAddr.Area);
        Assert.Equal(0xB4, wAddr.Code);
        Assert.Equal(0x1A, wAddr.Address);
        Assert.False(wAddr.IsBit);

        // 4. X 区 8 进制位输入
        Assert.True(MitsubishiMcDriver.TryParseAddress("X10", out var xAddr));
        Assert.Equal("X", xAddr.Area);
        Assert.Equal(0x9C, xAddr.Code);
        Assert.Equal(8, xAddr.Address); // 8进制 10 = 十进制 8
        Assert.True(xAddr.IsBit);
    }

    [Fact]
    public void MitsubishiMcDriver_BuildReadCommand_ShouldGenerateValid3EBinaryFrame()
    {
        MitsubishiMcDriver.TryParseAddress("D200", out var addr);
        var frame = MitsubishiMcDriver.BuildReadCommand(addr!, points: 10, netNo: 0, plcNo: 0xFF, targetIo: 0x03FF, stationNo: 0, timer: 0x0010);

        Assert.Equal(21, frame.Length);
        Assert.Equal(0x50, frame[0]); // 3E 请求帧头
        Assert.Equal(0x00, frame[1]);
        Assert.Equal(0x00, frame[2]); // 网络号
        Assert.Equal(0xFF, frame[3]); // PLC 号
        Assert.Equal(0xFF, frame[4]); // IO 0x03FF
        Assert.Equal(0x03, frame[5]);
        Assert.Equal(0x04, frame[11]); // 读命令 0x0104
        Assert.Equal(0x01, frame[12]);
        Assert.Equal(0x00, frame[13]); // 子命令字 0x0000
        Assert.Equal(0x00, frame[14]);
        Assert.Equal(200 & 0xFF, frame[15]); // 地址 200
        Assert.Equal(0xA8, frame[18]); // D 代码
        Assert.Equal(10, frame[19]); // 点数 10
    }

    [Fact]
    public void MitsubishiMcDriver_ParseResponse_ShouldValidateEndCodeAndPayload()
    {
        // 构造合法的 3E 成功响应：0xD0 0x00 00 FF FF 03 00 (Length=4) (EndCode=0x0000) (Data: 0x12, 0x34)
        byte[] validResp = new byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x04, 0x00, // 数据长 4 (2B EndCode + 2B Data)
            0x00, 0x00, // EndCode 0 成功
            0x12, 0x34  // 返回数据
        };

        bool success = MitsubishiMcDriver.ParseResponse(validResp, out var endCode, out var data);
        Assert.True(success);
        Assert.Equal(0, endCode);
        Assert.Equal(2, data.Length);
        Assert.Equal(0x12, data[0]);
        Assert.Equal(0x34, data[1]);

        // 构造异常响应 EndCode = 0xC059
        byte[] errorResp = new byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x02, 0x00,
            0x59, 0xC0
        };
        bool errSuccess = MitsubishiMcDriver.ParseResponse(errorResp, out var errCode, out _);
        Assert.False(errSuccess);
        Assert.Equal(0xC059, errCode);
    }

    #endregion

    #region 2. 欧姆龙 FINS 协议编解码与握手测试

    [Fact]
    public void OmronFinsDriver_AddressParser_ShouldParseFinsAddressesCorrectly()
    {
        // 1. DM 字
        Assert.True(OmronFinsDriver.TryParseAddress("DM200", out var dmAddr));
        Assert.Equal(0x82, dmAddr.Code);
        Assert.Equal(200, dmAddr.WordAddress);
        Assert.Equal(0, dmAddr.BitAddress);
        Assert.False(dmAddr.IsBit);

        // 2. DM 位
        Assert.True(OmronFinsDriver.TryParseAddress("DM200.5", out var dmBitAddr));
        Assert.Equal(0x02, dmBitAddr.Code);
        Assert.Equal(200, dmBitAddr.WordAddress);
        Assert.Equal(5, dmBitAddr.BitAddress);
        Assert.True(dmBitAddr.IsBit);

        // 3. CIO 字与位
        Assert.True(OmronFinsDriver.TryParseAddress("CIO100", out var cioAddr));
        Assert.Equal(0xB0, cioAddr.Code);
        Assert.Equal(100, cioAddr.WordAddress);
        Assert.False(cioAddr.IsBit);

        Assert.True(OmronFinsDriver.TryParseAddress("CIO100.12", out var cioBitAddr));
        Assert.Equal(0x30, cioBitAddr.Code);
        Assert.Equal(100, cioBitAddr.WordAddress);
        Assert.Equal(12, cioBitAddr.BitAddress);
        Assert.True(cioBitAddr.IsBit);
    }

    [Fact]
    public void OmronFinsDriver_TcpHandshake_ShouldBuildAndParseProperly()
    {
        var req = OmronFinsDriver.BuildTcpHandshakeRequest(pcNode: 10);
        Assert.Equal(20, req.Length);
        Assert.Equal((byte)'F', req[0]);
        Assert.Equal((byte)'I', req[1]);
        Assert.Equal((byte)'N', req[2]);
        Assert.Equal((byte)'S', req[3]);

        // 模拟服务器握手响应 (24 字节)
        byte[] serverResp = new byte[24];
        serverResp[0] = (byte)'F'; serverResp[1] = (byte)'I'; serverResp[2] = (byte)'N'; serverResp[3] = (byte)'S';
        // length = 16
        serverResp[7] = 16;
        // command = 1
        serverResp[11] = 1;
        // client node = 10
        serverResp[19] = 10;
        // server node = 1
        serverResp[23] = 1;

        bool parsed = OmronFinsDriver.ParseTcpHandshakeResponse(serverResp, out var clientNode, out var serverNode);
        Assert.True(parsed);
        Assert.Equal(10, clientNode);
        Assert.Equal(1, serverNode);
    }

    [Fact]
    public void OmronFinsDriver_BuildReadAndParseFinsResponse_ShouldWorkProperly()
    {
        OmronFinsDriver.TryParseAddress("DM50", out var addr);
        var readFrame = OmronFinsDriver.BuildReadCommand(addr!, points: 2, netNo: 0, plcNode: 1, pcNode: 10, sid: 5);

        Assert.Equal(18, readFrame.Length);
        Assert.Equal(0x80, readFrame[0]); // ICF
        Assert.Equal(0x02, readFrame[2]); // GCT
        Assert.Equal(1, readFrame[4]);    // DA1
        Assert.Equal(10, readFrame[7]);   // SA1
        Assert.Equal(5, readFrame[9]);    // SID
        Assert.Equal(0x01, readFrame[10]); // Command 0x0101
        Assert.Equal(0x01, readFrame[11]);
        Assert.Equal(0x82, readFrame[12]); // DM Code

        // 构造 FINS UDP 响应报文 (14 字节 Header/Command/EndCode + 4 字节数据)
        byte[] finsResp = new byte[18];
        finsResp[0] = 0xC0; // 响应 ICF
        finsResp[10] = 0x01; finsResp[11] = 0x01; // Command
        finsResp[12] = 0x00; finsResp[13] = 0x00; // EndCode 0 成功
        finsResp[14] = 0x00; finsResp[15] = 0x64; // Value 100
        finsResp[16] = 0x00; finsResp[17] = 0xC8; // Value 200

        bool ok = OmronFinsDriver.ParseFinsResponse(finsResp, isTcp: false, out var endCode, out var data);
        Assert.True(ok);
        Assert.Equal(0, endCode);
        Assert.Equal(4, data.Length);
        Assert.Equal(100, (data[0] << 8) | data[1]);
        Assert.Equal(200, (data[2] << 8) | data[3]);
    }

    #endregion

    #region 3. 驱动工厂动态解析测试

    [Fact]
    public void DefaultDriverFactory_ShouldResolveMitsubishiAndOmronDrivers()
    {
        var factory = new DefaultDriverFactory(null!);

        var mcDriver = factory.CreateDriver(ProtocolType.MitsubishiMc);
        Assert.NotNull(mcDriver);
        Assert.IsType<MitsubishiMcDriver>(mcDriver);
        Assert.Equal(ProtocolType.MitsubishiMc, mcDriver.ProtocolType);

        var finsDriver = factory.CreateDriver(ProtocolType.OmronFins);
        Assert.NotNull(finsDriver);
        Assert.IsType<OmronFinsDriver>(finsDriver);
        Assert.Equal(ProtocolType.OmronFins, finsDriver.ProtocolType);

        var protocols = factory.GetSupportedProtocols();
        Assert.Contains("MitsubishiMc", protocols);
        Assert.Contains("OmronFins", protocols);
    }

    #endregion

    #region 4. 工艺配方服务 (RecipeService) 持久化与增删改查测试

    [Fact]
    public async Task RecipeService_CrudAndPersistence_ShouldWorkCorrectly()
    {
        var scheduler = new DummyPriorityScheduler();
        var service = new RecipeService(scheduler, null, _connString);

        var recipe = new RecipeModel
        {
            RecipeId = "REC_TEST_001",
            Name = "SMT 高速贴片温控工艺配方",
            TargetDeviceId = "DEV_FURNACE_01",
            Version = "1.0.0",
            Items = new List<RecipeItem>
            {
                new("ZONE1_SETPOINT", 185.5, "温区1目标温度"),
                new("ZONE2_SETPOINT", 215.0, "温区2目标温度"),
                new("FAN_SPEED_RPM", 3600, "循环风机目标转速")
            }
        };

        // 1. 保存配方
        await service.SaveRecipeAsync(recipe);

        // 2. 根据设备读取配方
        var list = await service.GetRecipesByDeviceAsync("DEV_FURNACE_01");
        Assert.NotEmpty(list);
        var loaded = list.FirstOrDefault(r => r.RecipeId == "REC_TEST_001");
        Assert.NotNull(loaded);
        Assert.Equal("SMT 高速贴片温控工艺配方", loaded.Name);
        Assert.Equal(3, loaded.Items.Count);
        Assert.Equal("ZONE1_SETPOINT", loaded.Items[0].TagId);

        // 3. 删除配方
        await service.DeleteRecipeAsync("REC_TEST_001");
        var listAfterDel = await service.GetRecipesByDeviceAsync("DEV_FURNACE_01");
        Assert.DoesNotContain(listAfterDel, r => r.RecipeId == "REC_TEST_001");
    }

    #endregion

    #region 5. 合规审计日志服务 (AuditService) 记录与多维查询测试

    [Fact]
    public async Task AuditService_RecordAndQuery_ShouldPersistAndFilterAccurately()
    {
        var service = new AuditService(_connString);

        // 1. 便捷写入单条点位修改审计记录
        await service.RecordTagWriteAsync(
            operatorName: "Admin_Zhang",
            tagId: "DEV_01.Temp_Set",
            oldValue: 150.0,
            newValue: 180.0,
            isSuccess: true,
            elapsedMs: 12,
            error: null);

        // 2. 写入系统关键控制记录
        var customAudit = new AuditRecord
        {
            RecordId = "AUDIT_TEST_02",
            OperatorName = "Operator_Li",
            ActionType = "SystemControl",
            TargetId = "Main_Reactor",
            IsSuccess = true,
            ElapsedMs = 5,
            Timestamp = DateTime.Now
        };
        await service.RecordAsync(customAudit);

        // 3. 查询审计日志
        var logs = await service.QueryAuditLogsAsync(
            startTime: DateTime.Now.AddMinutes(-5),
            endTime: DateTime.Now.AddMinutes(5));

        Assert.True(logs.Count >= 2);

        // 按操作员过滤
        var zhangLogs = await service.QueryAuditLogsAsync(
            startTime: DateTime.Now.AddMinutes(-5),
            endTime: DateTime.Now.AddMinutes(5),
            operatorName: "Admin_Zhang");

        Assert.Single(zhangLogs);
        Assert.Equal("DEV_01.Temp_Set", zhangLogs[0].TargetId);
        Assert.Equal("150", zhangLogs[0].OldValue);
        Assert.Equal("180", zhangLogs[0].NewValue);
    }

    #endregion

    private class DummyPriorityScheduler : IPriorityScheduler
    {
        public bool IsRunning => true;
        public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public Task<WriteResult> EnqueueWriteAsync(string tagId, object value, CancellationToken ct = default) =>
            Task.FromResult(WriteResult.Success(tagId, value, 5));
        public Task<IReadOnlyDictionary<string, WriteResult>> EnqueueBatchWriteAsync(
            IEnumerable<KeyValuePair<string, object>> writes, CancellationToken ct = default)
        {
            var res = writes.ToDictionary(k => k.Key, v => WriteResult.Success(v.Key, v.Value, 5));
            return Task.FromResult<IReadOnlyDictionary<string, WriteResult>>(res);
        }
        public Task TriggerImmediatePollAsync(string deviceId) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
