using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Storage.Sqlite;

/// <summary>
/// 基于 SQLite 的用户与权限仓储实现 (含密码加盐哈希安全防护)
/// </summary>
public class SqliteUserRepository : IUserRepository
{
    private readonly string _connectionString;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SqliteUserRepository(string connectionString = "Data Source=scada_config.db")
    {
        _connectionString = connectionString;
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            const string createUsersTableSql = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    DisplayName TEXT NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    PasswordSalt TEXT NOT NULL,
                    Role INTEGER NOT NULL,
                    IsEnabled INTEGER NOT NULL,
                    LastLoginTime TEXT,
                    CreatedAt TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS UserViewPermissions (
                    Username TEXT NOT NULL,
                    ViewId TEXT NOT NULL,
                    PRIMARY KEY (Username, ViewId)
                );";

            var usersTableExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='Users';");
            if (usersTableExists == 0)
            {
                await connection.ExecuteAsync(createUsersTableSql);
            }
            else
            {
                var existingCols = (await connection.QueryAsync<string>(
                    "SELECT name FROM pragma_table_info('Users');")).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!existingCols.Contains("Id"))
                {
                    string tempOld = $"Users_old_{Guid.NewGuid():N}"[..20];
                    await connection.ExecuteAsync($"ALTER TABLE Users RENAME TO {tempOld};");
                    await connection.ExecuteAsync(createUsersTableSql);
                    const string cols = "Username, DisplayName, PasswordHash, PasswordSalt, Role, IsEnabled, LastLoginTime, CreatedAt";
                    await connection.ExecuteAsync($"INSERT INTO Users ({cols}) SELECT {cols} FROM {tempOld};");
                    await connection.ExecuteAsync($"DROP TABLE {tempOld};");
                }
                else
                {
                    await connection.ExecuteAsync(createUsersTableSql);
                }
            }

            // 检查是否存在初始管理员与预置账户
            var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Users;");
            if (count == 0)
            {
                var seedUsers = new List<(string u, string d, string p, UserRole r)>
                {
                    ("admin", "超级管理员", "admin888", UserRole.Administrator),
                    ("engineer", "现场工程师", "eng123", UserRole.Engineer),
                    ("operator", "产线操作员", "op123", UserRole.Operator)
                };

                const string insertUserSql = @"
                    INSERT INTO Users (Username, DisplayName, PasswordHash, PasswordSalt, Role, IsEnabled, CreatedAt)
                    VALUES (@Username, @DisplayName, @PasswordHash, @PasswordSalt, @Role, @IsEnabled, @CreatedAt);";

                foreach (var (u, d, p, r) in seedUsers)
                {
                    var (hash, salt) = HashPassword(p);
                    await connection.ExecuteAsync(insertUserSql, new UserInfo
                    {
                        Username = u,
                        DisplayName = d,
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        Role = r,
                        IsEnabled = true,
                        CreatedAt = DateTime.Now
                    });
                }
            }

            // 检查 UserViewPermissions 关联表，如果为空且存在 UiViews，自动为预置操作员和工程师分配初始可用画面
            var permCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM UserViewPermissions;");
            if (permCount == 0)
            {
                var tableExists = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='UiViews';");
                if (tableExists > 0)
                {
                    var viewIds = (await connection.QueryAsync<string>("SELECT ViewId FROM UiViews;")).ToList();
                    foreach (var vid in viewIds)
                    {
                        await connection.ExecuteAsync(
                            "INSERT OR IGNORE INTO UserViewPermissions (Username, ViewId) VALUES ('operator', @ViewId);", 
                            new { ViewId = vid });
                        await connection.ExecuteAsync(
                            "INSERT OR IGNORE INTO UserViewPermissions (Username, ViewId) VALUES ('engineer', @ViewId);", 
                            new { ViewId = vid });
                    }
                }
            }

            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<UserInfo?> GetUserAsync(string username)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM Users WHERE Username = @Username LIMIT 1;";
        return await connection.QueryFirstOrDefaultAsync<UserInfo>(sql, new { Username = username });
    }

    public async Task<UserInfo?> ValidateCredentialsAsync(string username, string plainPassword)
    {
        await EnsureInitializedAsync();
        var user = await GetUserAsync(username);
        if (user == null || !user.IsEnabled)
        {
            return null;
        }

        // 计算加盐哈希并验证
        if (VerifyPassword(plainPassword, user.PasswordHash, user.PasswordSalt))
        {
            await UpdateLastLoginTimeAsync(user.Username);
            user.LastLoginTime = DateTime.Now;
            return user;
        }

        return null;
    }

    public async Task SaveUserAsync(UserInfo user, string? newPlainPassword = null)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();

        if (!string.IsNullOrEmpty(newPlainPassword))
        {
            var (hash, salt) = HashPassword(newPlainPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
        }

        if (user.Id <= 0)
        {
            var existingCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Users WHERE Username = @Username;",
                new { user.Username });
            if (existingCount > 0)
            {
                throw new InvalidOperationException($"用户账号 [{user.Username}] 已存在，无法重复创建！");
            }

            const string sql = @"
                INSERT INTO Users (Username, DisplayName, PasswordHash, PasswordSalt, Role, IsEnabled, CreatedAt)
                VALUES (@Username, @DisplayName, @PasswordHash, @PasswordSalt, @Role, @IsEnabled, @CreatedAt);
                SELECT last_insert_rowid();";

            var newId = await connection.ExecuteScalarAsync<long>(sql, user);
            if (newId > 0)
            {
                user.Id = newId;
            }
        }
        else
        {
            // 更新用户：严格依据自增主键 WHERE Id = @Id
            const string sql = @"
                UPDATE Users SET
                    Username = @Username,
                    DisplayName = @DisplayName,
                    Role = @Role,
                    IsEnabled = @IsEnabled,
                    PasswordHash = CASE WHEN @PasswordHash != '' THEN @PasswordHash ELSE PasswordHash END,
                    PasswordSalt = CASE WHEN @PasswordSalt != '' THEN @PasswordSalt ELSE PasswordSalt END
                WHERE Id = @Id;";

            await connection.ExecuteAsync(sql, user);
        }
    }

    public async Task<bool> ChangePasswordAsync(string username, string newPlainPassword)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();

        var (hash, salt) = HashPassword(newPlainPassword);
        const string sql = @"
            UPDATE Users 
            SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt 
            WHERE Username = @Username;";

        int affected = await connection.ExecuteAsync(sql, new
        {
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt
        });

        return affected > 0;
    }

    public async Task UpdateLastLoginTimeAsync(string username)
    {
        await using var connection = CreateConnection();
        const string sql = "UPDATE Users SET LastLoginTime = @LastLoginTime WHERE Username = @Username;";
        await connection.ExecuteAsync(sql, new { Username = username, LastLoginTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
    }

    public async Task<IReadOnlyList<UserInfo>> GetAllUsersAsync()
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT * FROM Users ORDER BY Id;";
        var list = await connection.QueryAsync<UserInfo>(sql);
        return list.AsList();
    }

    public async Task<bool> DeleteUserAsync(string username)
    {
        if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            // 保护内置管理员不能被删除
            return false;
        }

        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        using var tx = await connection.BeginTransactionAsync();

        // 1. 级联清理授权记录
        await connection.ExecuteAsync("DELETE FROM UserViewPermissions WHERE Username = @Username;", new { Username = username }, tx);

        // 2. 删除用户
        int affected = await connection.ExecuteAsync("DELETE FROM Users WHERE Username = @Username;", new { Username = username }, tx);

        await tx.CommitAsync();
        return affected > 0;
    }

    public async Task<IReadOnlyList<string>> GetAllowedViewIdsAsync(string username)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        const string sql = "SELECT ViewId FROM UserViewPermissions WHERE Username = @Username ORDER BY ViewId;";
        var list = await connection.QueryAsync<string>(sql, new { Username = username });
        return list.AsList();
    }

    public async Task SetAllowedViewIdsAsync(string username, IEnumerable<string> viewIds)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        using var tx = await connection.BeginTransactionAsync();

        // 1. 清理已有授权
        await connection.ExecuteAsync("DELETE FROM UserViewPermissions WHERE Username = @Username;", new { Username = username }, tx);

        // 2. 插入新授权
        var distinctIds = viewIds.Distinct().ToList();
        if (distinctIds.Count > 0)
        {
            const string insertSql = "INSERT INTO UserViewPermissions (Username, ViewId) VALUES (@Username, @ViewId);";
            var paramList = distinctIds.Select(vid => new { Username = username, ViewId = vid });
            await connection.ExecuteAsync(insertSql, paramList, tx);
        }

        await tx.CommitAsync();
    }

    #region 密码加盐散列加密核心算法 (SHA256 + Salt)

    private static (string Hash, string Salt) HashPassword(string plainPassword)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
        string salt = Convert.ToBase64String(saltBytes);

        using var sha256 = SHA256.Create();
        byte[] combinedBytes = Encoding.UTF8.GetBytes(plainPassword + salt);
        byte[] hashBytes = sha256.ComputeHash(combinedBytes);
        string hash = Convert.ToBase64String(hashBytes);

        return (hash, salt);
    }

    private static bool VerifyPassword(string plainPassword, string storedHash, string storedSalt)
    {
        using var sha256 = SHA256.Create();
        byte[] combinedBytes = Encoding.UTF8.GetBytes(plainPassword + storedSalt);
        byte[] computedHash = sha256.ComputeHash(combinedBytes);
        string computedHashStr = Convert.ToBase64String(computedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHashStr),
            Encoding.UTF8.GetBytes(storedHash));
    }

    #endregion
}
