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
                    Username TEXT PRIMARY KEY,
                    DisplayName TEXT NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    PasswordSalt TEXT NOT NULL,
                    Role INTEGER NOT NULL,
                    IsEnabled INTEGER NOT NULL,
                    LastLoginTime TEXT,
                    CreatedAt TEXT NOT NULL
                );";
            await connection.ExecuteAsync(createUsersTableSql);

            // 检查是否存在初始管理员，若无则自动注入 admin / admin888
            var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Users;");
            if (count == 0)
            {
                var (hash, salt) = HashPassword("admin888");
                var adminUser = new UserInfo
                {
                    Username = "admin",
                    DisplayName = "超级管理员",
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    Role = UserRole.Administrator,
                    IsEnabled = true,
                    CreatedAt = DateTime.Now
                };

                const string insertAdminSql = @"
                    INSERT INTO Users (Username, DisplayName, PasswordHash, PasswordSalt, Role, IsEnabled, CreatedAt)
                    VALUES (@Username, @DisplayName, @PasswordHash, @PasswordSalt, @Role, @IsEnabled, @CreatedAt);";
                await connection.ExecuteAsync(insertAdminSql, adminUser);
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

        const string sql = @"
            INSERT INTO Users (Username, DisplayName, PasswordHash, PasswordSalt, Role, IsEnabled, CreatedAt)
            VALUES (@Username, @DisplayName, @PasswordHash, @PasswordSalt, @Role, @IsEnabled, @CreatedAt)
            ON CONFLICT(Username) DO UPDATE SET
                DisplayName = excluded.DisplayName,
                Role = excluded.Role,
                IsEnabled = excluded.IsEnabled,
                PasswordHash = CASE WHEN excluded.PasswordHash != '' THEN excluded.PasswordHash ELSE Users.PasswordHash END,
                PasswordSalt = CASE WHEN excluded.PasswordSalt != '' THEN excluded.PasswordSalt ELSE Users.PasswordSalt END;";

        await connection.ExecuteAsync(sql, user);
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
        const string sql = "SELECT * FROM Users ORDER BY Username;";
        var list = await connection.QueryAsync<UserInfo>(sql);
        return list.AsList();
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
