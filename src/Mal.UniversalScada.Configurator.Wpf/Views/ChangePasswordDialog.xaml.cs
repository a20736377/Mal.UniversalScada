using System.Windows;
using Mal.UniversalScada.Configurator.Wpf.Services;

namespace Mal.UniversalScada.Configurator.Wpf.Views;

public partial class ChangePasswordDialog : Window
{
    private readonly IAdminAuthService _authService;
    private readonly string _username;

    public ChangePasswordDialog(IAdminAuthService authService, string username)
    {
        InitializeComponent();
        _authService = authService;
        _username = username;

        Loaded += (_, _) => TxtOldPassword.Focus();
    }

    private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = string.Empty;

        string oldPwd = TxtOldPassword.Password;
        string newPwd = TxtNewPassword.Password;
        string confirmPwd = TxtConfirmPassword.Password;

        if (string.IsNullOrEmpty(oldPwd) || string.IsNullOrEmpty(newPwd))
        {
            TxtError.Text = "请输入原密码与新密码";
            return;
        }

        if (newPwd != confirmPwd)
        {
            TxtError.Text = "两次输入的新密码不一致";
            return;
        }

        var (success, message) = await _authService.ChangePasswordAsync(_username, oldPwd, newPwd);
        if (success)
        {
            MessageBox.Show(message, "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        else
        {
            TxtError.Text = message;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
