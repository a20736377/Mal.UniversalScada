using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Configurator.Wpf.Services;

/// <summary>
/// 管理员认证与权限服务接口
/// </summary>
public interface IAdminAuthService
{
    /// <summary>
    /// 当前已登录的管理员账户详细信息
    /// </summary>
    UserInfo? CurrentUserInfo { get; }

    /// <summary>
    /// 当前用户名
    /// </summary>
    string? CurrentUser => CurrentUserInfo?.Username;

    /// <summary>
    /// 是否已成功登录
    /// </summary>
    bool IsAuthenticated => CurrentUserInfo != null;

    /// <summary>
    /// 验证管理员登录凭据
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">明文密码</param>
    /// <returns>验证是否通过</returns>
    Task<bool> LoginAsync(string username, string password);

    /// <summary>
    /// 修改当前登录用户的密码
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="oldPassword">旧明文密码</param>
    /// <param name="newPassword">新明文密码</param>
    /// <returns>修改结果与错误描述</returns>
    Task<(bool Success, string Message)> ChangePasswordAsync(string username, string oldPassword, string newPassword);

    /// <summary>
    /// 注销登录
    /// </summary>
    void Logout();
}

/// <summary>
/// 基于 SQLite 用户仓储与加盐哈希的真实认证服务实现
/// </summary>
public class DefaultAdminAuthService : IAdminAuthService
{
    private readonly IUserRepository _userRepository;

    public UserInfo? CurrentUserInfo { get; private set; }

    public DefaultAdminAuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        var user = await _userRepository.ValidateCredentialsAsync(username.Trim(), password);
        if (user != null)
        {
            CurrentUserInfo = user;
            return true;
        }

        return false;
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(string username, string oldPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            return (false, "新密码长度不能少于 6 位字符");
        }

        // 先校验旧密码是否正确
        var user = await _userRepository.ValidateCredentialsAsync(username, oldPassword);
        if (user == null)
        {
            return (false, "原密码错误，身份验证失败");
        }

        bool result = await _userRepository.ChangePasswordAsync(username, newPassword);
        if (result)
        {
            return (true, "密码修改成功！请妥善保管新密码");
        }

        return (false, "修改密码失败，数据库写入异常");
    }

    public void Logout()
    {
        CurrentUserInfo = null;
    }
}
