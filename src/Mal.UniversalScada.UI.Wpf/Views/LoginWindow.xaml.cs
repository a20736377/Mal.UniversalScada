using System;
using System.Windows;
using System.Windows.Input;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.UI.Wpf.Views;

public partial class LoginWindow : Window
{
    private readonly IUserAuthService _authService;
    private readonly UserRole _requiredRole;

    public LoginWindow(IUserAuthService authService, UserRole requiredRole = UserRole.Operator, string? prompt = null)
    {
        InitializeComponent();
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _requiredRole = requiredRole;

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            PromptTextBlock.Text = prompt;
        }
        else
        {
            PromptTextBlock.Text = $"当前操作需要【{GetRoleDisplayName(requiredRole)}】或更高权限，请登录验证。";
        }

        UsernameBox.Focus();
    }

    private static string GetRoleDisplayName(UserRole role) => role switch
    {
        UserRole.Administrator => "系统管理员",
        UserRole.Engineer => "工程师",
        UserRole.Operator => "操作员",
        _ => "访客"
    };

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        ErrorTextBlock.Visibility = Visibility.Collapsed;
        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrEmpty(username))
        {
            ShowError("请输入用户名！");
            UsernameBox.Focus();
            return;
        }

        var (success, message) = await _authService.LoginAsync(username, password);
        if (!success)
        {
            ShowError(message);
            PasswordBox.SelectAll();
            PasswordBox.Focus();
            return;
        }

        var user = _authService.CurrentUser;
        // 校验是否满足所要求的权限
        if (user.Role < _requiredRole)
        {
            ShowError($"登录成功，但当前账户身份为【{GetRoleDisplayName(user.Role)}】，仍不足以执行此操作 (需【{GetRoleDisplayName(_requiredRole)}】)！");
            return;
        }

        DialogResult = true;
        Close();
    }

    private void ShowError(string msg)
    {
        ErrorTextBlock.Text = msg;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnLoginClick(this, new RoutedEventArgs());
        }
    }
}
