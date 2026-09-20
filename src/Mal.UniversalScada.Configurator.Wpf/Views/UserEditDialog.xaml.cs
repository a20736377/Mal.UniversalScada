using System;
using System.Collections.Generic;
using System.Windows;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Configurator.Wpf.Views;

public partial class UserEditDialog : Window
{
    private readonly bool _isEditMode;
    private readonly UserInfo? _originalUser;

    public UserInfo? ResultUser { get; private set; }
    public string? ResultPassword { get; private set; }

    public class RoleDisplayItem
    {
        public UserRole Role { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public UserEditDialog(UserInfo? userToEdit = null)
    {
        InitializeComponent();

        _originalUser = userToEdit;
        _isEditMode = userToEdit != null;

        var roles = new List<RoleDisplayItem>
        {
            new() { Role = UserRole.Operator, DisplayName = "操作员 (日常监视、按钮控制、配方下发)" },
            new() { Role = UserRole.Engineer, DisplayName = "工程师 (参数调试、限值修改、点位测试)" },
            new() { Role = UserRole.Administrator, DisplayName = "系统管理员 (全部权限、画面与用户管理)" }
        };

        CboRole.ItemsSource = roles;
        CboRole.DisplayMemberPath = "DisplayName";
        CboRole.SelectedValuePath = "Role";

        if (_isEditMode && userToEdit != null)
        {
            TxtTitle.Text = $"✏️ 编辑用户 [{userToEdit.Username}]";
            Title = $"编辑用户 - {userToEdit.Username}";
            TxtUsername.Text = userToEdit.Username;
            TxtUsername.IsEnabled = false;
            TxtDisplayName.Text = userToEdit.DisplayName;
            CboRole.SelectedValue = userToEdit.Role;
            ChkIsEnabled.IsChecked = userToEdit.IsEnabled;

            LblPasswordTip.Text = "重设密码 (留空表示不修改原密码):";
        }
        else
        {
            TxtTitle.Text = "➕ 新建监控系统用户";
            Title = "新建用户";
            CboRole.SelectedValue = UserRole.Operator;
            ChkIsEnabled.IsChecked = true;
            LblPasswordTip.Text = "设置登录密码 (必填，至少3位):";
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;
        var username = TxtUsername.Text.Trim();
        var displayName = TxtDisplayName.Text.Trim();
        var password = TxtPassword.Password;
        var confirmPassword = TxtConfirmPassword.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("登录用户名不能为空！");
            TxtUsername.Focus();
            return;
        }

        if (CboRole.SelectedValue is not UserRole role)
        {
            ShowError("请选择系统角色！");
            return;
        }

        if (!_isEditMode)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("新建用户必须输入初始登录密码！");
                TxtPassword.Focus();
                return;
            }

            if (password.Length < 3)
            {
                ShowError("登录密码长度至少为 3 个字符！");
                TxtPassword.Focus();
                return;
            }
        }

        if (!string.IsNullOrEmpty(password) && password != confirmPassword)
        {
            ShowError("两次输入的密码不一致，请核对！");
            TxtConfirmPassword.Focus();
            return;
        }

        ResultUser = new UserInfo
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName,
            Role = role,
            IsEnabled = ChkIsEnabled.IsChecked ?? true,
            CreatedAt = _originalUser?.CreatedAt ?? DateTime.Now,
            LastLoginTime = _originalUser?.LastLoginTime
        };

        ResultPassword = string.IsNullOrWhiteSpace(password) ? null : password;

        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        TxtError.Visibility = Visibility.Visible;
    }
}
