using System;
using System.Collections.Generic;
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

    #endregion

    private class InMemoryUserRepository : IUserRepository
    {
        private readonly List<UserInfo> _users = new()
        {
            new UserInfo { Username = "admin", PasswordHash = "admin888", Role = UserRole.Administrator, IsEnabled = true },
            new UserInfo { Username = "engineer", PasswordHash = "eng123", Role = UserRole.Engineer, IsEnabled = true },
            new UserInfo { Username = "operator", PasswordHash = "op123", Role = UserRole.Operator, IsEnabled = true }
        };

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
    }
}
