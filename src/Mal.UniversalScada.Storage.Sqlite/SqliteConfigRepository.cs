using Dapper;
using Microsoft.Data.Sqlite;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Storage.Sqlite;

/// <summary>
/// 基于 SQLite 的系统组态配置仓储实现 (IConfigRepository)
/// 负责通道 (Channels)、设备 (Devices)、点位 (Tags)、画布 (UiViews) 元数据的持久化读写与自增 ID 自动维护
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
    /// 确保数据库和所需数据表已初始化，具备自增 Id 字段，开启 WAL 模式提升读写并发
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

            // 2. 通道表 (Channels) - 自增 ID
            const string createChannelsSql = @"
                CREATE TABLE IF NOT EXISTS Channels (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ChannelId TEXT NOT NULL UNIQUE,
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
            await EnsureTableHasAutoincrementIdAsync(connection, "Channels", createChannelsSql, new[]
            {
                "ChannelId", "Name", "ChannelType", "PortName", "BaudRate", "DataBits", "StopBits", "Parity",
                "Host", "Port", "ReadTimeoutMs", "WriteTimeoutMs", "ReconnectIntervalMs", "IsEnabled"
            });

            // 3. 设备表 (Devices) - 自增 ID
            const string createDevicesSql = @"
                CREATE TABLE IF NOT EXISTS Devices (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeviceId TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    ChannelId TEXT NOT NULL,
                    ProtocolType INTEGER NOT NULL,
                    CustomProtocolName TEXT,
                    StationAddress INTEGER NOT NULL,
                    DefaultPollIntervalMs INTEGER NOT NULL,
                    TimeoutMs INTEGER NOT NULL,
                    IsEnabled INTEGER NOT NULL
                );";
            await EnsureTableHasAutoincrementIdAsync(connection, "Devices", createDevicesSql, new[]
            {
                "DeviceId", "Name", "ChannelId", "ProtocolType", "CustomProtocolName", "StationAddress",
                "DefaultPollIntervalMs", "TimeoutMs", "IsEnabled"
            });

            // 4. 点位表 (Tags) - 自增 ID (无 TagId)
            const string createTagsSql = @"
                CREATE TABLE IF NOT EXISTS Tags (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
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
            await EnsureTableHasAutoincrementIdAsync(connection, "Tags", createTagsSql, new[]
            {
                "DeviceId", "Name", "Address", "DataType", "AccessMode", "ScaleFactor",
                "Offset", "Unit", "ScanIntervalMs", "Deadband", "IsHistorical"
            });
            await connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Tags_DeviceId ON Tags(DeviceId);");

            // 5. 画面视图组态表 (UiViews) - 自增 ID
            const string createUiViewsSql = @"
                CREATE TABLE IF NOT EXISTS UiViews (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ViewId TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    BoundDeviceId TEXT,
                    LayoutMode TEXT NOT NULL,
                    CanvasWidth REAL NOT NULL DEFAULT 1920,
                    CanvasHeight REAL NOT NULL DEFAULT 1080,
                    BackgroundColor TEXT DEFAULT '#0F172A',
                    BackgroundImagePath TEXT,
                    BackgroundImageStretch TEXT DEFAULT 'UniformToFill',
                    BackgroundImageOpacity REAL DEFAULT 1.0,
                    WidgetsJson TEXT NOT NULL,
                    IsDefault INTEGER NOT NULL DEFAULT 0,
                    UpdatedTime TEXT NOT NULL
                );";
            await EnsureTableHasAutoincrementIdAsync(connection, "UiViews", createUiViewsSql, new[]
            {
                "ViewId", "Name", "BoundDeviceId", "LayoutMode", "CanvasWidth", "CanvasHeight",
                "BackgroundColor", "BackgroundImagePath", "BackgroundImageStretch", "BackgroundImageOpacity",
                "WidgetsJson", "IsDefault", "UpdatedTime"
            });

            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task EnsureTableHasAutoincrementIdAsync(
        SqliteConnection connection,
        string tableName,
        string createNewTableSql,
        string[] targetCols)
    {
        var tableExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@TableName;",
            new { TableName = tableName });

        if (tableExists == 0)
        {
            await connection.ExecuteAsync(createNewTableSql);
            return;
        }

        var existingCols = (await connection.QueryAsync<string>(
            $"SELECT name FROM pragma_table_info('{tableName}');")).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existingCols.Contains("Id") || (tableName == "Tags" && existingCols.Contains("TagId")))
        {
            string tempOld = $"{tableName}_old_{Guid.NewGuid():N}"[..20];
            await connection.ExecuteAsync($"ALTER TABLE {tableName} RENAME TO {tempOld};");
            await connection.ExecuteAsync(createNewTableSql);

            var commonCols = targetCols.Where(c => existingCols.Contains(c)).ToList();
            if (existingCols.Contains("Id") && !commonCols.Contains("Id"))
            {
                commonCols.Insert(0, "Id");
            }
            var colsStr = string.Join(", ", commonCols);
            await connection.ExecuteAsync($"INSERT INTO {tableName} ({colsStr}) SELECT {colsStr} FROM {tempOld};");
            await connection.ExecuteAsync($"DROP TABLE {tempOld};");
        }
    }

    #region 通道配置管理 (Channels)

    public async Task<IReadOnlyList<ChannelConfig>> GetChannelsAsync()
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM Channels ORDER BY Id;";
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

        if (channel.Id <= 0)
        {
            // 新增通道：检查 ChannelId 是否为空或是否已存在于数据库中
            bool needsAutoId = string.IsNullOrWhiteSpace(channel.ChannelId);
            if (!needsAutoId)
            {
                var existingCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM Channels WHERE ChannelId = @ChannelId;",
                    new { channel.ChannelId });
                if (existingCount > 0)
                {
                    needsAutoId = true; // 存在同名冲突，重新由自增 ID 分配
                }
            }

            string tempChannelId = needsAutoId ? $"CH_TEMP_{Guid.NewGuid():N}"[..16] : channel.ChannelId;

            const string insertSql = @"
                INSERT INTO Channels (
                    ChannelId, Name, ChannelType, PortName, BaudRate, DataBits, StopBits, Parity,
                    Host, Port, ReadTimeoutMs, WriteTimeoutMs, ReconnectIntervalMs, IsEnabled
                ) VALUES (
                    @ChannelId, @Name, @ChannelType, @PortName, @BaudRate, @DataBits, @StopBits, @Parity,
                    @Host, @Port, @ReadTimeoutMs, @WriteTimeoutMs, @ReconnectIntervalMs, @IsEnabled
                );
                SELECT last_insert_rowid();";

            var newId = await connection.ExecuteScalarAsync<long>(insertSql, new
            {
                ChannelId = tempChannelId,
                channel.Name,
                channel.ChannelType,
                channel.PortName,
                channel.BaudRate,
                channel.DataBits,
                channel.StopBits,
                channel.Parity,
                channel.Host,
                channel.Port,
                channel.ReadTimeoutMs,
                channel.WriteTimeoutMs,
                channel.ReconnectIntervalMs,
                channel.IsEnabled
            });

            if (newId > 0)
            {
                channel.Id = newId;
                if (needsAutoId)
                {
                    channel.ChannelId = $"CH_{newId:D3}";
                    await connection.ExecuteAsync(
                        "UPDATE Channels SET ChannelId = @ChannelId WHERE Id = @Id;",
                        new { channel.ChannelId, channel.Id });
                }
            }
        }
        else
        {
            // 更新通道：严格使用自增主键 WHERE Id = @Id，绝不通过 string id 匹配！
            const string updateSql = @"
                UPDATE Channels SET
                    ChannelId = @ChannelId,
                    Name = @Name,
                    ChannelType = @ChannelType,
                    PortName = @PortName,
                    BaudRate = @BaudRate,
                    DataBits = @DataBits,
                    StopBits = @StopBits,
                    Parity = @Parity,
                    Host = @Host,
                    Port = @Port,
                    ReadTimeoutMs = @ReadTimeoutMs,
                    WriteTimeoutMs = @WriteTimeoutMs,
                    ReconnectIntervalMs = @ReconnectIntervalMs,
                    IsEnabled = @IsEnabled
                WHERE Id = @Id;";

            await connection.ExecuteAsync(updateSql, channel);
        }
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
        const string sql = "SELECT * FROM Devices ORDER BY Id;";
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
        const string sql = "SELECT * FROM Devices WHERE ChannelId = @ChannelId ORDER BY Id;";
        var list = await connection.QueryAsync<DeviceNode>(sql, new { ChannelId = channelId });
        return list.AsList();
    }

    public async Task SaveDeviceAsync(DeviceNode device)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();

        if (device.Id <= 0)
        {
            // 新增设备：检查 DeviceId 是否为空或是否已存在于数据库中
            bool needsAutoId = string.IsNullOrWhiteSpace(device.DeviceId);
            if (!needsAutoId)
            {
                var existingCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM Devices WHERE DeviceId = @DeviceId;",
                    new { device.DeviceId });
                if (existingCount > 0)
                {
                    needsAutoId = true; // 存在同名冲突，重新由自增 ID 统一分配
                }
            }

            string tempDeviceId = needsAutoId ? $"DEV_TEMP_{Guid.NewGuid():N}"[..16] : device.DeviceId;

            const string insertSql = @"
                INSERT INTO Devices (
                    DeviceId, Name, ChannelId, ProtocolType, CustomProtocolName, StationAddress,
                    DefaultPollIntervalMs, TimeoutMs, IsEnabled
                ) VALUES (
                    @DeviceId, @Name, @ChannelId, @ProtocolType, @CustomProtocolName, @StationAddress,
                    @DefaultPollIntervalMs, @TimeoutMs, @IsEnabled
                );
                SELECT last_insert_rowid();";

            var newId = await connection.ExecuteScalarAsync<long>(insertSql, new
            {
                DeviceId = tempDeviceId,
                device.Name,
                device.ChannelId,
                device.ProtocolType,
                device.CustomProtocolName,
                device.StationAddress,
                device.DefaultPollIntervalMs,
                device.TimeoutMs,
                device.IsEnabled
            });

            if (newId > 0)
            {
                device.Id = newId;
                if (needsAutoId)
                {
                    device.DeviceId = $"DEV_{newId:D2}";
                    await connection.ExecuteAsync(
                        "UPDATE Devices SET DeviceId = @DeviceId WHERE Id = @Id;",
                        new { device.DeviceId, device.Id });
                }
            }
        }
        else
        {
            // 更新设备：严格使用自增主键 WHERE Id = @Id，绝不通过 string id 匹配！
            const string updateSql = @"
                UPDATE Devices SET
                    DeviceId = @DeviceId,
                    Name = @Name,
                    ChannelId = @ChannelId,
                    ProtocolType = @ProtocolType,
                    CustomProtocolName = @CustomProtocolName,
                    StationAddress = @StationAddress,
                    DefaultPollIntervalMs = @DefaultPollIntervalMs,
                    TimeoutMs = @TimeoutMs,
                    IsEnabled = @IsEnabled
                WHERE Id = @Id;";

            await connection.ExecuteAsync(updateSql, device);
        }
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
        const string sql = "SELECT * FROM Tags WHERE DeviceId = @DeviceId ORDER BY Id;";
        var list = await connection.QueryAsync<TagNode>(sql, new { DeviceId = deviceId });
        return list.AsList();
    }

    public async Task<IReadOnlyList<TagNode>> GetAllTagsAsync()
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM Tags ORDER BY DeviceId, Id;";
        var list = await connection.QueryAsync<TagNode>(sql);
        return list.AsList();
    }

    public async Task SaveTagAsync(TagNode tag)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();

        if (tag.Id <= 0)
        {
            const string insertSql = @"
                INSERT INTO Tags (
                    DeviceId, Name, Address, DataType, AccessMode, ScaleFactor,
                    Offset, Unit, ScanIntervalMs, Deadband, IsHistorical
                ) VALUES (
                    @DeviceId, @Name, @Address, @DataType, @AccessMode, @ScaleFactor,
                    @Offset, @Unit, @ScanIntervalMs, @Deadband, @IsHistorical
                );
                SELECT last_insert_rowid();";

            var newId = await connection.ExecuteScalarAsync<long>(insertSql, new
            {
                tag.DeviceId,
                tag.Name,
                tag.Address,
                tag.DataType,
                tag.AccessMode,
                tag.ScaleFactor,
                tag.Offset,
                tag.Unit,
                tag.ScanIntervalMs,
                tag.Deadband,
                tag.IsHistorical
            });

            if (newId > 0)
            {
                tag.Id = newId;
            }
        }
        else
        {
            // 更新点位：严格使用自增主键 WHERE Id = @Id
            const string updateSql = @"
                UPDATE Tags SET
                    DeviceId = @DeviceId,
                    Name = @Name,
                    Address = @Address,
                    DataType = @DataType,
                    AccessMode = @AccessMode,
                    ScaleFactor = @ScaleFactor,
                    Offset = @Offset,
                    Unit = @Unit,
                    ScanIntervalMs = @ScanIntervalMs,
                    Deadband = @Deadband,
                    IsHistorical = @IsHistorical
                WHERE Id = @Id;";

            await connection.ExecuteAsync(updateSql, tag);
        }
    }

    public async Task BatchSaveTagsAsync(IEnumerable<TagNode> tags)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        const string insertSql = @"
            INSERT INTO Tags (
                DeviceId, Name, Address, DataType, AccessMode, ScaleFactor,
                Offset, Unit, ScanIntervalMs, Deadband, IsHistorical
            ) VALUES (
                @DeviceId, @Name, @Address, @DataType, @AccessMode, @ScaleFactor,
                @Offset, @Unit, @ScanIntervalMs, @Deadband, @IsHistorical
            );
            SELECT last_insert_rowid();";

        const string updateSql = @"
            UPDATE Tags SET
                DeviceId = @DeviceId,
                Name = @Name,
                Address = @Address,
                DataType = @DataType,
                AccessMode = @AccessMode,
                ScaleFactor = @ScaleFactor,
                Offset = @Offset,
                Unit = @Unit,
                ScanIntervalMs = @ScanIntervalMs,
                Deadband = @Deadband,
                IsHistorical = @IsHistorical
            WHERE Id = @Id;";

        foreach (var tag in tags)
        {
            if (tag.Id <= 0)
            {
                var newId = await connection.ExecuteScalarAsync<long>(insertSql, new
                {
                    tag.DeviceId,
                    tag.Name,
                    tag.Address,
                    tag.DataType,
                    tag.AccessMode,
                    tag.ScaleFactor,
                    tag.Offset,
                    tag.Unit,
                    tag.ScanIntervalMs,
                    tag.Deadband,
                    tag.IsHistorical
                }, transaction);

                if (newId > 0)
                {
                    tag.Id = newId;
                }
            }
            else
            {
                // 更新时严格使用 WHERE Id = @Id
                await connection.ExecuteAsync(updateSql, tag, transaction);
            }
        }

        transaction.Commit();
    }

    public async Task DeleteTagAsync(long id)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "DELETE FROM Tags WHERE Id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    #endregion

    #region 界面组态视图 (UiViews)

    private class UiViewDto
    {
        public long Id { get; set; }
        public string ViewId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? BoundDeviceId { get; set; }
        public string LayoutMode { get; set; } = "Canvas";
        public double CanvasWidth { get; set; } = 1920;
        public double CanvasHeight { get; set; } = 1080;
        public string BackgroundColor { get; set; } = "#0F172A";
        public string? BackgroundImagePath { get; set; }
        public string BackgroundImageStretch { get; set; } = "UniformToFill";
        public double BackgroundImageOpacity { get; set; } = 1.0;
        public string WidgetsJson { get; set; } = "[]";
        public int IsDefault { get; set; }
        public string UpdatedTime { get; set; } = string.Empty;

        public UiViewConfig ToModel()
        {
            List<WidgetConfig> widgets;
            try
            {
                widgets = System.Text.Json.JsonSerializer.Deserialize<List<WidgetConfig>>(WidgetsJson) ?? new();
            }
            catch
            {
                widgets = new();
            }

            DateTime.TryParse(UpdatedTime, out var dt);

            return new UiViewConfig
            {
                Id = Id,
                ViewId = ViewId,
                Name = Name,
                BoundDeviceId = BoundDeviceId,
                LayoutMode = LayoutMode,
                CanvasWidth = CanvasWidth > 0 ? CanvasWidth : 1920,
                CanvasHeight = CanvasHeight > 0 ? CanvasHeight : 1080,
                BackgroundColor = string.IsNullOrWhiteSpace(BackgroundColor) ? "#0F172A" : BackgroundColor,
                BackgroundImagePath = BackgroundImagePath,
                BackgroundImageStretch = string.IsNullOrWhiteSpace(BackgroundImageStretch) ? "UniformToFill" : BackgroundImageStretch,
                BackgroundImageOpacity = BackgroundImageOpacity <= 0 ? 1.0 : BackgroundImageOpacity,
                IsDefault = IsDefault == 1,
                Widgets = widgets,
                UpdatedTime = dt == default ? DateTime.Now : dt
            };
        }
    }

    public async Task<IReadOnlyList<UiViewConfig>> GetUiViewsAsync()
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM UiViews ORDER BY IsDefault DESC, Id ASC;";
        var dtos = await connection.QueryAsync<UiViewDto>(sql);
        return dtos.Select(d => d.ToModel()).ToList();
    }

    public async Task<UiViewConfig?> GetUiViewByIdAsync(string viewId)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM UiViews WHERE ViewId = @ViewId LIMIT 1;";
        var dto = await connection.QueryFirstOrDefaultAsync<UiViewDto>(sql, new { ViewId = viewId });
        return dto?.ToModel();
    }

    public async Task SaveUiViewAsync(UiViewConfig view)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        if (view.IsDefault)
        {
            // 若设为默认，先清空其它画面的默认标识
            await connection.ExecuteAsync("UPDATE UiViews SET IsDefault = 0;", transaction);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(view.Widgets);

        if (view.Id <= 0)
        {
            bool needsAutoId = string.IsNullOrWhiteSpace(view.ViewId);
            if (!needsAutoId)
            {
                var existingCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM UiViews WHERE ViewId = @ViewId;",
                    new { view.ViewId }, transaction);
                if (existingCount > 0)
                {
                    needsAutoId = true;
                }
            }

            string tempViewId = needsAutoId ? $"VIEW_TEMP_{Guid.NewGuid():N}"[..16] : view.ViewId;

            const string insertSql = @"
                INSERT INTO UiViews (
                    ViewId, Name, BoundDeviceId, LayoutMode, CanvasWidth, CanvasHeight,
                    BackgroundColor, BackgroundImagePath, BackgroundImageStretch, BackgroundImageOpacity,
                    WidgetsJson, IsDefault, UpdatedTime
                ) VALUES (
                    @ViewId, @Name, @BoundDeviceId, @LayoutMode, @CanvasWidth, @CanvasHeight,
                    @BackgroundColor, @BackgroundImagePath, @BackgroundImageStretch, @BackgroundImageOpacity,
                    @WidgetsJson, @IsDefault, @UpdatedTime
                );
                SELECT last_insert_rowid();";

            var newId = await connection.ExecuteScalarAsync<long>(insertSql, new
            {
                ViewId = tempViewId,
                view.Name,
                view.BoundDeviceId,
                view.LayoutMode,
                view.CanvasWidth,
                view.CanvasHeight,
                BackgroundColor = view.BackgroundColor ?? "#0F172A",
                view.BackgroundImagePath,
                BackgroundImageStretch = view.BackgroundImageStretch ?? "UniformToFill",
                BackgroundImageOpacity = view.BackgroundImageOpacity <= 0 ? 1.0 : view.BackgroundImageOpacity,
                WidgetsJson = json,
                IsDefault = view.IsDefault ? 1 : 0,
                UpdatedTime = view.UpdatedTime.ToString("yyyy-MM-dd HH:mm:ss")
            }, transaction);

            if (newId > 0)
            {
                view.Id = newId;
                if (needsAutoId)
                {
                    view.ViewId = $"VIEW_{newId:D3}";
                    await connection.ExecuteAsync(
                        "UPDATE UiViews SET ViewId = @ViewId WHERE Id = @Id;",
                        new { view.ViewId, view.Id }, transaction);
                }
            }
        }
        else
        {
            // 更新画面：严格使用自增主键 WHERE Id = @Id
            const string updateSql = @"
                UPDATE UiViews SET
                    ViewId = @ViewId,
                    Name = @Name,
                    BoundDeviceId = @BoundDeviceId,
                    LayoutMode = @LayoutMode,
                    CanvasWidth = @CanvasWidth,
                    CanvasHeight = @CanvasHeight,
                    BackgroundColor = @BackgroundColor,
                    BackgroundImagePath = @BackgroundImagePath,
                    BackgroundImageStretch = @BackgroundImageStretch,
                    BackgroundImageOpacity = @BackgroundImageOpacity,
                    WidgetsJson = @WidgetsJson,
                    IsDefault = @IsDefault,
                    UpdatedTime = @UpdatedTime
                WHERE Id = @Id;";

            await connection.ExecuteAsync(updateSql, new
            {
                view.Id,
                view.ViewId,
                view.Name,
                view.BoundDeviceId,
                view.LayoutMode,
                view.CanvasWidth,
                view.CanvasHeight,
                BackgroundColor = view.BackgroundColor ?? "#0F172A",
                view.BackgroundImagePath,
                BackgroundImageStretch = view.BackgroundImageStretch ?? "UniformToFill",
                BackgroundImageOpacity = view.BackgroundImageOpacity <= 0 ? 1.0 : view.BackgroundImageOpacity,
                WidgetsJson = json,
                IsDefault = view.IsDefault ? 1 : 0,
                UpdatedTime = view.UpdatedTime.ToString("yyyy-MM-dd HH:mm:ss")
            }, transaction);
        }

        await transaction.CommitAsync();
    }

    public async Task DeleteUiViewAsync(string viewId)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "DELETE FROM UiViews WHERE ViewId = @ViewId;";
        await connection.ExecuteAsync(sql, new { ViewId = viewId });
    }

    #endregion
}
