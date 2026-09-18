using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mal.UniversalScada.Configurator.Wpf.Services;

namespace Mal.UniversalScada.Configurator.Wpf.ViewModels;

/// <summary>
/// 管理员登录视图模型
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAdminAuthService _authService;

    [ObservableProperty]
    private string _username = "admin";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// 登录成功回调通知
    /// </summary>
    public event Action? LoginSucceeded;

    public LoginViewModel(IAdminAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "请输入管理员账号与密码";
            return;
        }

        try
        {
            IsBusy = true;
            bool success = await _authService.LoginAsync(Username, Password);
            if (success)
            {
                LoginSucceeded?.Invoke();
            }
            else
            {
                ErrorMessage = "账号或密码错误，请核对后重试";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"登录发生异常: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
