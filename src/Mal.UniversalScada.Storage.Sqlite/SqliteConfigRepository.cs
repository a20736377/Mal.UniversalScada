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

            // 5. 创建画面视图组态表 (UiViews)
            const string createUiViewsSql = @"
                CREATE TABLE IF NOT EXISTS UiViews (
                    ViewId TEXT PRIMARY KEY,
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
            await connection.ExecuteAsync(createUiViewsSql);

            // 数据库轻量级自动迁移
            try { await connection.ExecuteAsync("ALTER TABLE UiViews ADD COLUMN BackgroundColor TEXT DEFAULT '#0F172A';"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE UiViews ADD COLUMN BackgroundImagePath TEXT;"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE UiViews ADD COLUMN BackgroundImageStretch TEXT DEFAULT 'UniformToFill';"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE UiViews ADD COLUMN BackgroundImageOpacity REAL DEFAULT 1.0;"); } catch { }

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

    #region 界面组态视图 (UiViews)

    private class UiViewDto
    {
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
        const string sql = "SELECT * FROM UiViews ORDER BY IsDefault DESC, UpdatedTime DESC;";
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

        const string sql = @"
            INSERT INTO UiViews (
                ViewId, Name, BoundDeviceId, LayoutMode, CanvasWidth, CanvasHeight,
                BackgroundColor, BackgroundImagePath, BackgroundImageStretch, BackgroundImageOpacity,
                WidgetsJson, IsDefault, UpdatedTime
            ) VALUES (
                @ViewId, @Name, @BoundDeviceId, @LayoutMode, @CanvasWidth, @CanvasHeight,
                @BackgroundColor, @BackgroundImagePath, @BackgroundImageStretch, @BackgroundImageOpacity,
                @WidgetsJson, @IsDefault, @UpdatedTime
            )
            ON CONFLICT(ViewId) DO UPDATE SET
                Name = excluded.Name,
                BoundDeviceId = excluded.BoundDeviceId,
                LayoutMode = excluded.LayoutMode,
                CanvasWidth = excluded.CanvasWidth,
                CanvasHeight = excluded.CanvasHeight,
                BackgroundColor = excluded.BackgroundColor,
                BackgroundImagePath = excluded.BackgroundImagePath,
                BackgroundImageStretch = excluded.BackgroundImageStretch,
                BackgroundImageOpacity = excluded.BackgroundImageOpacity,
                WidgetsJson = excluded.WidgetsJson,
                IsDefault = excluded.IsDefault,
                UpdatedTime = excluded.UpdatedTime;";

        await connection.ExecuteAsync(sql, new
        {
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
