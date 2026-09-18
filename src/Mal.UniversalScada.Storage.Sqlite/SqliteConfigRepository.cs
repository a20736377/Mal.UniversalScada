using Dapper;
using Microsoft.Data.Sqlite;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Storage.Sqlite;

/// <summary>
/// 基于 SQLite 的系统组态配置仓储实现 (IConfigRepository)
/// 负责通道 (Channels)、设备 (Devices)、点位 (Tags) 元数据的持久化读写
/// </summary>
public class SqliteConfigRepository : IConfigRepository
{
    private readonly string _connectionString;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SqliteConfigRepository(string connectionString = "Data Source=scada_config.db")
    {
        _connectionString = connectionString;
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    /// <summary>
    /// 确保数据库和所需数据表已初始化，开启 WAL 模式提升读写并发
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            // 1. 开启 WAL 模式与同步优化
            await connection.ExecuteAsync("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;");

            // 2. 创建通道表 (Channels)
            const string createChannelsSql = @"
                CREATE TABLE IF NOT EXISTS Channels (
                    ChannelId TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    ChannelType INTEGER NOT NULL,
                    PortName TEXT,
                    BaudRate INTEGER NOT NULL,
                    DataBits INTEGER NOT NULL,
                    StopBits TEXT,
                    Parity TEXT,
                    Host TEXT,
                    Port INTEGER NOT NULL,
                    ReadTimeoutMs INTEGER NOT NULL,
                    WriteTimeoutMs INTEGER NOT NULL,
                    ReconnectIntervalMs INTEGER NOT NULL,
                    IsEnabled INTEGER NOT NULL
                );";
            await connection.ExecuteAsync(createChannelsSql);

            // 3. 创建设备表 (Devices)
            const string createDevicesSql = @"
                CREATE TABLE IF NOT EXISTS Devices (
                    DeviceId TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    ChannelId TEXT NOT NULL,
                    ProtocolType INTEGER NOT NULL,
                    CustomProtocolName TEXT,
                    StationAddress INTEGER NOT NULL,
                    DefaultPollIntervalMs INTEGER NOT NULL,
                    TimeoutMs INTEGER NOT NULL,
                    IsEnabled INTEGER NOT NULL
                );";
            await connection.ExecuteAsync(createDevicesSql);

            // 4. 创建点位表 (Tags)
            const string createTagsSql = @"
                CREATE TABLE IF NOT EXISTS Tags (
                    TagId TEXT PRIMARY KEY,
                    DeviceId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Address TEXT NOT NULL,
                    DataType INTEGER NOT NULL,
                    AccessMode INTEGER NOT NULL,
                    ScaleFactor REAL NOT NULL,
                    Offset REAL NOT NULL,
                    Unit TEXT,
                    ScanIntervalMs INTEGER NOT NULL,
                    Deadband REAL NOT NULL,
                    IsHistorical INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_Tags_DeviceId ON Tags(DeviceId);";
            await connection.ExecuteAsync(createTagsSql);

            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    #region 通道配置管理 (Channels)

    public async Task<IReadOnlyList<ChannelConfig>> GetChannelsAsync()
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM Channels ORDER BY ChannelId;";
        var list = await connection.QueryAsync<ChannelConfig>(sql);
        return list.AsList();
    }

    public async Task<ChannelConfig?> GetChannelByIdAsync(string channelId)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM Channels WHERE ChannelId = @ChannelId LIMIT 1;";
        return await connection.QueryFirstOrDefaultAsync<ChannelConfig>(sql, new { ChannelId = channelId });
    }

    public async Task SaveChannelAsync(ChannelConfig channel)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = @"
            INSERT INTO Channels (
                ChannelId, Name, ChannelType, PortName, BaudRate, DataBits, StopBits, Parity,
                Host, Port, ReadTimeoutMs, WriteTimeoutMs, ReconnectIntervalMs, IsEnabled
            ) VALUES (
                @ChannelId, @Name, @ChannelType, @PortName, @BaudRate, @DataBits, @StopBits, @Parity,
                @Host, @Port, @ReadTimeoutMs, @WriteTimeoutMs, @ReconnectIntervalMs, @IsEnabled
            )
            ON CONFLICT(ChannelId) DO UPDATE SET
                Name = excluded.Name,
                ChannelType = excluded.ChannelType,
                PortName = excluded.PortName,
                BaudRate = excluded.BaudRate,
                DataBits = excluded.DataBits,
                StopBits = excluded.StopBits,
                Parity = excluded.Parity,
                Host = excluded.Host,
                Port = excluded.Port,
                ReadTimeoutMs = excluded.ReadTimeoutMs,
                WriteTimeoutMs = excluded.WriteTimeoutMs,
                ReconnectIntervalMs = excluded.ReconnectIntervalMs,
                IsEnabled = excluded.IsEnabled;";

        // 依据传输介质类型，不需要设置的字段保存成空
        if (channel.ChannelType == ChannelType.SerialPort)
        {
            channel.Host = string.Empty;
            channel.Port = 0;
        }
        else
        {
            channel.PortName = string.Empty;
            channel.BaudRate = 0;
            channel.DataBits = 0;
            channel.StopBits = string.Empty;
            channel.Parity = string.Empty;
        }

        await connection.ExecuteAsync(sql, channel);
    }

    public async Task DeleteChannelAsync(string channelId)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "DELETE FROM Channels WHERE ChannelId = @ChannelId;";
        await connection.ExecuteAsync(sql, new { ChannelId = channelId });
    }

    #endregion

    #region 设备节点管理 (Devices)

    public async Task<IReadOnlyList<DeviceNode>> GetDevicesAsync()
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM Devices ORDER BY DeviceId;";
        var list = await connection.QueryAsync<DeviceNode>(sql);
        return list.AsList();
    }

    public async Task<DeviceNode?> GetDeviceByIdAsync(string deviceId)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM Devices WHERE DeviceId = @DeviceId LIMIT 1;";
        return await connection.QueryFirstOrDefaultAsync<DeviceNode>(sql, new { DeviceId = deviceId });
    }

    public async Task<IReadOnlyList<DeviceNode>> GetDevicesByChannelAsync(string channelId)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM Devices WHERE ChannelId = @ChannelId ORDER BY DeviceId;";
        var list = await connection.QueryAsync<DeviceNode>(sql, new { ChannelId = channelId });
        return list.AsList();
    }

    public async Task SaveDeviceAsync(DeviceNode device)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = @"
            INSERT INTO Devices (
                DeviceId, Name, ChannelId, ProtocolType, CustomProtocolName, StationAddress,
                DefaultPollIntervalMs, TimeoutMs, IsEnabled
            ) VALUES (
                @DeviceId, @Name, @ChannelId, @ProtocolType, @CustomProtocolName, @StationAddress,
                @DefaultPollIntervalMs, @TimeoutMs, @IsEnabled
            )
            ON CONFLICT(DeviceId) DO UPDATE SET
                Name = excluded.Name,
                ChannelId = excluded.ChannelId,
                ProtocolType = excluded.ProtocolType,
                CustomProtocolName = excluded.CustomProtocolName,
                StationAddress = excluded.StationAddress,
                DefaultPollIntervalMs = excluded.DefaultPollIntervalMs,
                TimeoutMs = excluded.TimeoutMs,
                IsEnabled = excluded.IsEnabled;";

        await connection.ExecuteAsync(sql, device);
    }

    public async Task DeleteDeviceAsync(string deviceId)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "DELETE FROM Devices WHERE DeviceId = @DeviceId;";
        await connection.ExecuteAsync(sql, new { DeviceId = deviceId });
    }

    #endregion

    #region 点位组态管理 (Tags)

    public async Task<IReadOnlyList<TagNode>> GetTagsByDeviceAsync(string deviceId)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM Tags WHERE DeviceId = @DeviceId ORDER BY TagId;";
        var list = await connection.QueryAsync<TagNode>(sql, new { DeviceId = deviceId });
        return list.AsList();
    }

    public async Task<IReadOnlyList<TagNode>> GetAllTagsAsync()
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM Tags ORDER BY DeviceId, TagId;";
        var list = await connection.QueryAsync<TagNode>(sql);
        return list.AsList();
    }

    public async Task SaveTagAsync(TagNode tag)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = @"
            INSERT INTO Tags (
                TagId, DeviceId, Name, Address, DataType, AccessMode, ScaleFactor,
                Offset, Unit, ScanIntervalMs, Deadband, IsHistorical
            ) VALUES (
                @TagId, @DeviceId, @Name, @Address, @DataType, @AccessMode, @ScaleFactor,
                @Offset, @Unit, @ScanIntervalMs, @Deadband, @IsHistorical
            )
            ON CONFLICT(TagId) DO UPDATE SET
                DeviceId = excluded.DeviceId,
                Name = excluded.Name,
                Address = excluded.Address,
                DataType = excluded.DataType,
                AccessMode = excluded.AccessMode,
                ScaleFactor = excluded.ScaleFactor,
                Offset = excluded.Offset,
                Unit = excluded.Unit,
                ScanIntervalMs = excluded.ScanIntervalMs,
                Deadband = excluded.Deadband,
                IsHistorical = excluded.IsHistorical;";

        await connection.ExecuteAsync(sql, tag);
    }

    public async Task BatchSaveTagsAsync(IEnumerable<TagNode> tags)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        const string sql = @"
            INSERT INTO Tags (
                TagId, DeviceId, Name, Address, DataType, AccessMode, ScaleFactor,
                Offset, Unit, ScanIntervalMs, Deadband, IsHistorical
            ) VALUES (
                @TagId, @DeviceId, @Name, @Address, @DataType, @AccessMode, @ScaleFactor,
                @Offset, @Unit, @ScanIntervalMs, @Deadband, @IsHistorical
            )
            ON CONFLICT(TagId) DO UPDATE SET
                DeviceId = excluded.DeviceId,
                Name = excluded.Name,
                Address = excluded.Address,
                DataType = excluded.DataType,
                AccessMode = excluded.AccessMode,
                ScaleFactor = excluded.ScaleFactor,
                Offset = excluded.Offset,
                Unit = excluded.Unit,
                ScanIntervalMs = excluded.ScanIntervalMs,
                Deadband = excluded.Deadband,
                IsHistorical = excluded.IsHistorical;";

        await connection.ExecuteAsync(sql, tags, transaction);
        transaction.Commit();
    }

    public async Task DeleteTagAsync(string tagId)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "DELETE FROM Tags WHERE TagId = @TagId;";
        await connection.ExecuteAsync(sql, new { TagId = tagId });
    }

    #endregion
}
