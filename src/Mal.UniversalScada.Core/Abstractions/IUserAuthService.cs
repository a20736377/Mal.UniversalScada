using System;
using System.Threading.Tasks;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 全局用户身份认证与权限管理服务接口。
/// 统一管理访客、操作员、工程师与超级管理员的会话生命周期与操作越权拦截。
/// </summary>
public interface IUserAuthService
{
    /// <summary>
    /// 获取当前会话生效的用户信息（默认未登录时为 Guest 访客角色）
    /// </summary>
    UserInfo CurrentUser { get; }

    /// <summary>
    /// 是否已通过密码验证登录（即角色高于访客）
    /// </summary>
    bool IsAuthenticated => CurrentUser.Role > UserRole.Guest;

    /// <summary>
    /// 验证用户凭据并切换当前会话身份
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码明文</param>
    /// <returns>登录是否成功及反馈描述</returns>
    Task<(bool Success, string Message)> LoginAsync(string username, string password);

    /// <summary>
    /// 注销当前会话，安全恢复为 Guest 访客模式
    /// </summary>
    void Logout();

    /// <summary>
    /// 校验当前用户是否具有指定级别的操作权限
    /// </summary>
    /// <param name="requiredRole">所需最低权限角色等级</param>
    /// <returns>是否具备权限</returns>
    bool CheckPermission(UserRole requiredRole);

    /// <summary>
    /// 当用户登录、切换或注销退出时触发
    /// </summary>
    event EventHandler<UserInfo>? CurrentUserChanged;
}
