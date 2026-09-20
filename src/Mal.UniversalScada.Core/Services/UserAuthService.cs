using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Services;

/// <summary>
/// 全局用户认证与权限授权服务实现。
/// </summary>
public class UserAuthService : IUserAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditService? _auditService;
    private UserInfo _currentUser;
    private bool _isPreloaded;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    public UserInfo CurrentUser => _currentUser;

    /// <inheritdoc />
    public event EventHandler<UserInfo>? CurrentUserChanged;

    public UserAuthService(IUserRepository userRepository, IAuditService? auditService = null)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _auditService = auditService;
        _currentUser = UserInfo.CreateGuest();
    }

    private async Task EnsureDefaultUsersAsync()
    {
        if (_isPreloaded) return;
        await _lock.WaitAsync();
        try
        {
            if (_isPreloaded) return;

            var all = await _userRepository.GetAllUsersAsync();
            if (all.Count == 0)
            {
                // 初始化预制三大核心账户
                await _userRepository.SaveUserAsync(new UserInfo
                {
                    Username = "admin",
                    DisplayName = "系统超级管理员",
                    Role = UserRole.Administrator,
                    IsEnabled = true
                }, "admin888");

                await _userRepository.SaveUserAsync(new UserInfo
                {
                    Username = "engineer",
                    DisplayName = "现场电气工程师",
                    Role = UserRole.Engineer,
                    IsEnabled = true
                }, "eng123");

                await _userRepository.SaveUserAsync(new UserInfo
                {
                    Username = "operator",
                    DisplayName = "产线操作员",
                    Role = UserRole.Operator,
                    IsEnabled = true
                }, "op123");
            }

            _isPreloaded = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return (false, "用户名或密码不能为空。");
        }

        await EnsureDefaultUsersAsync();

        var sw = Stopwatch.StartNew();
        try
        {
            var user = await _userRepository.ValidateCredentialsAsync(username, password);
            sw.Stop();

            if (user != null)
            {
                if (!user.IsEnabled)
                {
                    await RecordAuditAsync(username, "Login", false, "该账户已被停用，禁止登录。");
                    return (false, "该账户已被停用，请联系管理员。");
                }

                _currentUser = user;
                await _userRepository.UpdateLastLoginTimeAsync(username);
                CurrentUserChanged?.Invoke(this, _currentUser);

                await RecordAuditAsync(username, "Login", true, $"角色登录成功: {user.DisplayName} [{user.Role}]");
                return (true, $"欢迎登录，{user.DisplayName}！");
            }
            else
            {
                await RecordAuditAsync(username, "Login", false, "用户名或密码验证失败。");
                return (false, "用户名或密码错误，请核对后重试。");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            await RecordAuditAsync(username, "Login", false, ex.Message);
            return (false, $"登录验证异常: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void Logout()
    {
        var prevName = _currentUser.Username;
        _currentUser = UserInfo.CreateGuest();
        CurrentUserChanged?.Invoke(this, _currentUser);

        _ = RecordAuditAsync(prevName, "Logout", true, "操作员注销退出，系统恢复访客只读模式。");
    }

    /// <inheritdoc />
    public bool CheckPermission(UserRole requiredRole)
    {
        return _currentUser.HasPermission(requiredRole);
    }

    private async Task RecordAuditAsync(string operatorName, string actionType, bool success, string desc)
    {
        if (_auditService != null)
        {
            try
            {
                await _auditService.RecordAsync(new AuditRecord
                {
                    OperatorName = operatorName,
                    ActionType = actionType,
                    TargetId = "UserSession",
                    IsSuccess = success,
                    ErrorMessage = success ? null : desc,
                    Timestamp = DateTime.Now
                });
            }
            catch { }
        }
    }
}
