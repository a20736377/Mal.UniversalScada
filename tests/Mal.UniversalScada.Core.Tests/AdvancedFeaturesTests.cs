using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Core.Services;

namespace Mal.UniversalScada.Core.Tests;

public class AdvancedFeaturesTests
{
    #region 0. 数据库定位器测试

    [Fact]
    public void ScadaDatabaseLocator_ResolvesRootDatabasePath()
    {
        var dbPath = Mal.UniversalScada.Storage.Sqlite.ScadaDatabaseLocator.GetDatabaseFilePath();
        Assert.NotNull(dbPath);
        Assert.EndsWith("scada_config.db", dbPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(System.IO.File.Exists(dbPath), $"数据库文件应该存在: {dbPath}");
    }

    #endregion

    #region 1. 撤销重做 (Undo / Redo) 测试

    [Fact]
    public void UndoRedo_StackOperations_RestorePreviousStateAccurately()
    {
        // 模拟设计器历史快照栈
        var undoStack = new Stack<List<WidgetConfig>>();
        var redoStack = new Stack<List<WidgetConfig>>();

        var state1 = new List<WidgetConfig>
        {
            new() { WidgetId = "W1", Type = WidgetType.NumericCard, X = 10, Y = 10 }
        };
        var state2 = new List<WidgetConfig>
        {
            new() { WidgetId = "W1", Type = WidgetType.NumericCard, X = 50, Y = 50 },
            new() { WidgetId = "W2", Type = WidgetType.Valve, X = 100, Y = 100 }
        };

        // 压入状态 1
        undoStack.Push(state1);

        // 此时当前状态为 state2，执行 Undo
        Assert.True(undoStack.Count > 0);
        redoStack.Push(state2);
        var restored1 = undoStack.Pop();

        Assert.Single(restored1);
        Assert.Equal("W1", restored1[0].WidgetId);
        Assert.Equal(10, restored1[0].X);

        // 执行 Redo
        Assert.True(redoStack.Count > 0);
        undoStack.Push(restored1);
        var restored2 = redoStack.Pop();

        Assert.Equal(2, restored2.Count);
        Assert.Equal("W2", restored2[1].WidgetId);
        Assert.Equal(WidgetType.Valve, restored2[1].Type);
    }

    [Fact]
    public void UndoRedo_MaxStackDepth_Enforced()
    {
        var undoStack = new Stack<int>();
        const int maxDepth = 10;

        for (int i = 0; i < 20; i++)
        {
            undoStack.Push(i);
            if (undoStack.Count > maxDepth)
            {
                var list = undoStack.ToList();
                list.RemoveAt(list.Count - 1);
                undoStack.Clear();
                for (int j = list.Count - 1; j >= 0; j--) undoStack.Push(list[j]);
            }
        }

        Assert.Equal(maxDepth, undoStack.Count);
        Assert.Equal(19, undoStack.Peek());
    }

    #endregion

    #region 2. 成组与解组 (Group / Ungroup) 测试

    [Fact]
    public void GroupUngroup_WidgetConfig_GroupIdPreserved()
    {
        var w1 = new WidgetConfig { WidgetId = "W1", Type = WidgetType.Valve, GroupId = "grp_test1" };
        var w2 = new WidgetConfig { WidgetId = "W2", Type = WidgetType.Pump, GroupId = "grp_test1" };
        var w3 = new WidgetConfig { WidgetId = "W3", Type = WidgetType.Pipe };

        // 验证成组归属
        Assert.Equal("grp_test1", w1.GroupId);
        Assert.Equal("grp_test1", w2.GroupId);
        Assert.Null(w3.GroupId);

        // 模拟解组
        var widgets = new List<WidgetConfig> { w1, w2, w3 };
        var targetGroupIds = widgets.Where(w => w.GroupId != null).Select(w => w.GroupId).Distinct().ToHashSet();
        foreach (var w in widgets)
        {
            if (w.GroupId != null && targetGroupIds.Contains(w.GroupId))
            {
                w.GroupId = null;
            }
        }

        Assert.Null(w1.GroupId);
        Assert.Null(w2.GroupId);
        Assert.Null(w3.GroupId);
    }

    #endregion

    #region 3. 专用阀门与离心旋转泵图元 (Valve / Pump) 测试

    [Fact]
    public void WidgetType_ValveAndPump_EnumValuesAndProperties()
    {
        var valve = new WidgetConfig
        {
            WidgetId = "V1",
            Type = WidgetType.Valve,
            Title = "主蒸汽隔离阀",
            Width = 140,
            Height = 90
        };
        valve.Properties["IsOpen"] = "True";
        valve.Properties["Orientation"] = "Horizontal";
        valve.Properties["OpenColor"] = "#22C55E";

        var pump = new WidgetConfig
        {
            WidgetId = "P1",
            Type = WidgetType.Pump,
            Title = "冷却水离心循环泵",
            Width = 140,
            Height = 140
        };
        pump.Properties["IsRunning"] = "True";
        pump.Properties["SpeedRpm"] = "1450";
        pump.Properties["RunningColor"] = "#0EA5E9";

        Assert.Equal(WidgetType.Valve, valve.Type);
        Assert.Equal("True", valve.Properties["IsOpen"]);
        Assert.Equal(WidgetType.Pump, pump.Type);
        Assert.Equal("1450", pump.Properties["SpeedRpm"]);
    }

    #endregion

    #region 4. 用户角色鉴权与权限拦截 (Auth / RBAC) 测试

    [Theory]
    [InlineData(UserRole.Guest, UserRole.Guest, true)]
    [InlineData(UserRole.Guest, UserRole.Operator, false)]
    [InlineData(UserRole.Guest, UserRole.Engineer, false)]
    [InlineData(UserRole.Guest, UserRole.Administrator, false)]
    [InlineData(UserRole.Operator, UserRole.Guest, true)]
    [InlineData(UserRole.Operator, UserRole.Operator, true)]
    [InlineData(UserRole.Operator, UserRole.Engineer, false)]
    [InlineData(UserRole.Engineer, UserRole.Operator, true)]
    [InlineData(UserRole.Engineer, UserRole.Engineer, true)]
    [InlineData(UserRole.Engineer, UserRole.Administrator, false)]
    [InlineData(UserRole.Administrator, UserRole.Administrator, true)]
    [InlineData(UserRole.Administrator, UserRole.Operator, true)]
    public void UserRole_HasPermission_MonotonicHierarchy(UserRole currentRole, UserRole requiredRole, bool expected)
    {
        var user = new UserInfo
        {
            Username = "TestUser",
            Role = currentRole,
            IsEnabled = true
        };

        Assert.Equal(expected, user.HasPermission(requiredRole));
    }

    [Fact]
    public async Task UserAuthService_DefaultAccountsAndLoginLogout_WorkProperly()
    {
        var repo = new InMemoryUserRepository();
        var audit = new AuditService();
        var authService = new UserAuthService(repo, audit);

        // 1. 默认初始状态为 Guest
        Assert.Equal("guest", authService.CurrentUser.Username);
        Assert.Equal(UserRole.Guest, authService.CurrentUser.Role);
        Assert.False(authService.CheckPermission(UserRole.Operator));

        // 2. 错误密码拒绝
        var (fail, failMsg) = await authService.LoginAsync("operator", "wrong_pass");
        Assert.False(fail);
        Assert.Contains("错误", failMsg);
        Assert.Equal(UserRole.Guest, authService.CurrentUser.Role);

        // 3. 正确凭证登录 operator
        var (okOp, _) = await authService.LoginAsync("operator", "op123");
        Assert.True(okOp);
        Assert.Equal(UserRole.Operator, authService.CurrentUser.Role);
        Assert.True(authService.CheckPermission(UserRole.Operator));
        Assert.False(authService.CheckPermission(UserRole.Engineer));

        // 4. 切换登录 admin
        var (okAdmin, _) = await authService.LoginAsync("admin", "admin888");
        Assert.True(okAdmin);
        Assert.Equal(UserRole.Administrator, authService.CurrentUser.Role);
        Assert.True(authService.CheckPermission(UserRole.Administrator));
        Assert.True(authService.CheckPermission(UserRole.Engineer));
        Assert.True(authService.CheckPermission(UserRole.Operator));

        // 5. 注销后恢复 Guest
        authService.Logout();
        Assert.Equal("guest", authService.CurrentUser.Username);
        Assert.Equal(UserRole.Guest, authService.CurrentUser.Role);
        Assert.False(authService.CheckPermission(UserRole.Operator));
    }

    [Fact]
    public async Task UserCrudAndPermissions_AddUpdateDisableDeleteAndMultiToMultiViews_WorkCorrectly()
    {
        var repo = new InMemoryUserRepository();

        // 1. 添加新用户
        var newUser = new UserInfo
        {
            Username = "tech01",
            DisplayName = "产线技术员1",
            Role = UserRole.Operator,
            IsEnabled = true
        };
        await repo.SaveUserAsync(newUser, "pwd123");

        var fetched = await repo.GetUserAsync("tech01");
        Assert.NotNull(fetched);
        Assert.Equal("产线技术员1", fetched.DisplayName);
        Assert.True(fetched.IsEnabled);

        // 2. 禁用用户
        fetched.IsEnabled = false;
        await repo.SaveUserAsync(fetched);
        var disabled = await repo.GetUserAsync("tech01");
        Assert.False(disabled?.IsEnabled);

        // 禁用后密码验证应返回 null
        var valResult = await repo.ValidateCredentialsAsync("tech01", "pwd123");
        Assert.Null(valResult);

        // 重新启用
        fetched.IsEnabled = true;
        await repo.SaveUserAsync(fetched);
        var valSuccess = await repo.ValidateCredentialsAsync("tech01", "pwd123");
        Assert.NotNull(valSuccess);

        // 3. 多对多画面授权测试
        var allowedViews = new List<string> { "View_Reflow_Oven", "View_Filling_Station" };
        await repo.SetAllowedViewIdsAsync("tech01", allowedViews);

        var retrievedViews = await repo.GetAllowedViewIdsAsync("tech01");
        Assert.Equal(2, retrievedViews.Count);
        Assert.Contains("View_Reflow_Oven", retrievedViews);
        Assert.Contains("View_Filling_Station", retrievedViews);

        // 更新授权（移除一个，增加另一个）
        await repo.SetAllowedViewIdsAsync("tech01", new[] { "View_Reflow_Oven", "View_Packaging" });
        var updatedViews = await repo.GetAllowedViewIdsAsync("tech01");
        Assert.Equal(2, updatedViews.Count);
        Assert.Contains("View_Packaging", updatedViews);
        Assert.DoesNotContain("View_Filling_Station", updatedViews);

        // 4. 超级管理员安全保护（禁止删除 admin）
        var deleteAdmin = await repo.DeleteUserAsync("admin");
        Assert.False(deleteAdmin);
        Assert.NotNull(await repo.GetUserAsync("admin"));

        // 5. 删除普通用户，级联清理其画面方案授权
        var deleteTech = await repo.DeleteUserAsync("tech01");
        Assert.True(deleteTech);
        Assert.Null(await repo.GetUserAsync("tech01"));

        var viewsAfterDelete = await repo.GetAllowedViewIdsAsync("tech01");
        Assert.Empty(viewsAfterDelete);
    }

    [Fact]
    public async Task SqliteUserRepository_FullLifecycleAndPermissions_Succeeds()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"scada_test_{Guid.NewGuid():N}.db");
        var connStr = $"Data Source={tempDb}";
        try
        {
            var sqliteRepo = new Mal.UniversalScada.Storage.Sqlite.SqliteUserRepository(connStr);

            // 1. 验证预置种子用户存在
            var allUsers = await sqliteRepo.GetAllUsersAsync();
            Assert.Contains(allUsers, u => u.Username == "admin");
            Assert.Contains(allUsers, u => u.Username == "engineer");
            Assert.Contains(allUsers, u => u.Username == "operator");

            // 2. 验证默认 admin 密码验证 (admin888)
            var adminAuth = await sqliteRepo.ValidateCredentialsAsync("admin", "admin888");
            Assert.NotNull(adminAuth);
            Assert.Equal(UserRole.Administrator, adminAuth.Role);

            // 3. 新建操作员用户
            var testUser = new UserInfo
            {
                Username = "op_line2",
                DisplayName = "二车间操作员",
                Role = UserRole.Operator,
                IsEnabled = true
            };
            await sqliteRepo.SaveUserAsync(testUser, "secret456");

            var validated = await sqliteRepo.ValidateCredentialsAsync("op_line2", "secret456");
            Assert.NotNull(validated);
            Assert.Equal("二车间操作员", validated.DisplayName);

            // 4. 画面方案多对多授权
            await sqliteRepo.SetAllowedViewIdsAsync("op_line2", new[] { "View_01", "View_02" });
            var allowed = await sqliteRepo.GetAllowedViewIdsAsync("op_line2");
            Assert.Equal(2, allowed.Count);
            Assert.Contains("View_01", allowed);
            Assert.Contains("View_02", allowed);

            // 5. 尝试删除系统超级管理员 admin，应受到保护返回 false
            var delAdmin = await sqliteRepo.DeleteUserAsync("admin");
            Assert.False(delAdmin);
            Assert.NotNull(await sqliteRepo.GetUserAsync("admin"));

            // 6. 正常删除用户，并级联清理画面权限
            var delUser = await sqliteRepo.DeleteUserAsync("op_line2");
            Assert.True(delUser);
            Assert.Null(await sqliteRepo.GetUserAsync("op_line2"));
            var remainingPerms = await sqliteRepo.GetAllowedViewIdsAsync("op_line2");
            Assert.Empty(remainingPerms);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    #endregion

    private class InMemoryUserRepository : IUserRepository
    {
        private readonly List<UserInfo> _users = new()
        {
            new UserInfo { Username = "admin", PasswordHash = "admin888", Role = UserRole.Administrator, IsEnabled = true },
            new UserInfo { Username = "engineer", PasswordHash = "eng123", Role = UserRole.Engineer, IsEnabled = true },
            new UserInfo { Username = "operator", PasswordHash = "op123", Role = UserRole.Operator, IsEnabled = true }
        };

        private readonly Dictionary<string, HashSet<string>> _userViewPermissions = new(StringComparer.OrdinalIgnoreCase);

        public Task<UserInfo?> GetUserAsync(string username) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)));

        public Task<UserInfo?> ValidateCredentialsAsync(string username, string plainPassword)
        {
            var user = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (user != null && user.IsEnabled && user.PasswordHash == plainPassword)
            {
                return Task.FromResult<UserInfo?>(user);
            }
            return Task.FromResult<UserInfo?>(null);
        }

        public Task SaveUserAsync(UserInfo user, string? newPlainPassword = null)
        {
            if (newPlainPassword != null) user.PasswordHash = newPlainPassword;
            _users.RemoveAll(u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase));
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task<bool> ChangePasswordAsync(string username, string newPlainPassword)
        {
            var user = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (user != null)
            {
                user.PasswordHash = newPlainPassword;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task UpdateLastLoginTimeAsync(string username)
        {
            var user = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (user != null) user.LastLoginTime = DateTime.Now;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UserInfo>> GetAllUsersAsync() =>
            Task.FromResult<IReadOnlyList<UserInfo>>(_users);

        public Task<bool> DeleteUserAsync(string username)
        {
            if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(false);
            }

            var removed = _users.RemoveAll(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            _userViewPermissions.Remove(username);
            return Task.FromResult(removed > 0);
        }

        public Task<IReadOnlyList<string>> GetAllowedViewIdsAsync(string username)
        {
            if (_userViewPermissions.TryGetValue(username, out var set))
            {
                return Task.FromResult<IReadOnlyList<string>>(set.ToList());
            }
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task SetAllowedViewIdsAsync(string username, IEnumerable<string> viewIds)
        {
            _userViewPermissions[username] = new HashSet<string>(viewIds, StringComparer.OrdinalIgnoreCase);
            return Task.CompletedTask;
        }
    }
}
