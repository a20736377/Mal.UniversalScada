using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 系统用户与权限仓储接口
/// 负责用户账户、密码验证及角色权限的持久化管理
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// 根据用户名获取账户信息
    /// </summary>
    /// <param name="username">用户名</param>
    Task<UserInfo?> GetUserAsync(string username);

    /// <summary>
    /// 验证用户名与密码是否匹配
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="plainPassword">明文密码</param>
    /// <returns>若验证成功返回账户信息，失败或禁用返回 null</returns>
    Task<UserInfo?> ValidateCredentialsAsync(string username, string plainPassword);

    /// <summary>
    /// 保存或创建新用户 (若存在则更新基本信息)
    /// </summary>
    Task SaveUserAsync(UserInfo user, string? newPlainPassword = null);

    /// <summary>
    /// 修改指定用户的登录密码
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="newPlainPassword">新明文密码</param>
    Task<bool> ChangePasswordAsync(string username, string newPlainPassword);

    /// <summary>
    /// 更新用户最后一次登录时间
    /// </summary>
    Task UpdateLastLoginTimeAsync(string username);

    /// <summary>
    /// 获取系统中所有注册的用户列表
    /// </summary>
    Task<IReadOnlyList<UserInfo>> GetAllUsersAsync();

    /// <summary>
    /// 删除指定用户 (级联清理其所有画面授权映射)
    /// </summary>
    /// <param name="username">待删除的用户名</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteUserAsync(string username);

    /// <summary>
    /// 获取指定用户被授权的可视化画面方案 ID 列表 (多对多关系)
    /// </summary>
    /// <param name="username">用户名</param>
    Task<IReadOnlyList<string>> GetAllowedViewIdsAsync(string username);

    /// <summary>
    /// 批量设置指定用户的可视化画面方案授权集合 (多对多关系)
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="viewIds">授权的画面方案 ID 集合</param>
    Task SetAllowedViewIdsAsync(string username, IEnumerable<string> viewIds);
}
