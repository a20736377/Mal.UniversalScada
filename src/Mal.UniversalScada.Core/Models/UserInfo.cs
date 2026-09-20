namespace Mal.UniversalScada.Core.Models;

/// <summary>
/// 系统用户角色定义 (数值越大权限越高)
/// </summary>
public enum UserRole
{
    /// <summary>
    /// 参观者 / 访客 (未登录或只读，仅允许浏览监控画面，禁止下发任何控制指令与修改配置)
    /// </summary>
    Guest = 0,

    /// <summary>
    /// 操作员 (允许日常设备启停、控制按钮下发、工艺配方下发与报警确认)
    /// </summary>
    Operator = 1,

    /// <summary>
    /// 现场工程师 (除操作员权限外，允许调整工程量参数、报警限值与点位调试)
    /// </summary>
    Engineer = 2,

    /// <summary>
    /// 系统超级管理员 (拥有全部权限，可配置通信拓扑、修改系统参数、进入设计器、管理用户账号)
    /// </summary>
    Administrator = 3
}

/// <summary>
/// 用户账户实体模型
/// </summary>
public class UserInfo
{
    /// <summary>
    /// 用户自增数字主键 ID (系统自动递增)
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 登录用户名 (唯一账户名)
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

    /// <summary>
    /// 检查当前用户是否具备满足指定要求的最低权限
    /// </summary>
    public bool HasPermission(UserRole requiredRole) => IsEnabled && Role >= requiredRole;

    /// <summary>
    /// 创建默认未登录访客对象
    /// </summary>
    public static UserInfo CreateGuest() => new()
    {
        Username = "guest",
        DisplayName = "访客 (只读)",
        Role = UserRole.Guest,
        IsEnabled = true
    };
}
