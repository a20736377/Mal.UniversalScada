using System.Windows;
using System.Windows.Input;
using Mal.UniversalScada.Configurator.Wpf.ViewModels;

namespace Mal.UniversalScada.Configurator.Wpf.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.LoginSucceeded += OnLoginSucceeded;

        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(TxtUsername.Text))
            {
                TxtUsername.Focus();
            }
            else
            {
                TxtPassword.Focus();
            }
        };
    }

    private void TxtUsername_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TxtPassword.Focus();
        }
    }

    private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = TxtPassword.Password;
    }

    private void OnLoginSucceeded()
    {
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
