namespace Mal.UniversalScada.Core.Models;

/// <summary>
/// 系统用户角色定义
/// </summary>
public enum UserRole
{
    /// <summary>
    /// 只读操作员 (仅可查看监控与点位)
    /// </summary>
    Operator = 0,

    /// <summary>
    /// 现场工程师 (可调参、下发配方、调试点位)
    /// </summary>
    Engineer = 1,

    /// <summary>
    /// 系统超级管理员 (拥有全部权限，可配置通信拓扑、修改系统参数、管理用户账号)
    /// </summary>
    Administrator = 2
}

/// <summary>
/// 用户账户实体模型
/// </summary>
public class UserInfo
{
    /// <summary>
    /// 登录用户名 (主键，唯一)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 显示姓名或操作员名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 加密后的密码散列哈希值
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 密码随机加盐值
    /// </summary>
    public string PasswordSalt { get; set; } = string.Empty;

    /// <summary>
    /// 用户角色与权限等级
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Administrator;

    /// <summary>
    /// 账户是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 最后一次成功登录时间
    /// </summary>
    public DateTime? LastLoginTime { get; set; }

    /// <summary>
    /// 账户创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
