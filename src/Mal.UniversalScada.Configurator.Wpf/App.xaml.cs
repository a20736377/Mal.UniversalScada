using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mal.UniversalScada.Configurator.Wpf.ViewModels;
using Mal.UniversalScada.Configurator.Wpf.Views;
using Mal.UniversalScada.Core.Configuration;
using Mal.UniversalScada.Drivers.Siemens;
using Mal.UniversalScada.Storage.Sqlite;

namespace Mal.UniversalScada.Configurator.Wpf;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 核心关键：设置手动/显式控制退出模式，防止登录窗口关闭时直接退出整个应用程序
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 注册全局异常捕获，防止未知错误导致程序静默退出
        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"程序运行发生未处理异常: {args.Exception.Message}\n\n{args.Exception.StackTrace}", 
                "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // 1. 组态配置底层数据库仓储 (SQLite)
                    services.AddSqliteConfigStorage("Data Source=scada_config.db");

                    // 2. 核心组态业务服务 (认证、组态引擎、导入导出、硬件连通性探测、通道与驱动工厂、点位测试器)
                    services.AddScadaConfigurationCore();
                    services.AddSiemensS7Driver();

                    // 3. ViewModel
                    services.AddTransient<LoginViewModel>();
                    services.AddSingleton<MainViewModel>();

                    // 5. 视图窗口
                    services.AddTransient<LoginWindow>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await AppHost.StartAsync();

            // 步骤 1：先弹出管理员登录弹窗
            var loginWindow = AppHost.Services.GetRequiredService<LoginWindow>();
            bool? loginSuccess = loginWindow.ShowDialog();

            if (loginSuccess == true)
            {
                // 步骤 2：验证通过，加载主配置界面，恢复主窗体关闭时自动退出
                var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
            }
            else
            {
                // 取消登录或关闭登录弹窗，退出程序
                Shutdown();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"应用程序初始化失败: {ex.Message}\n\n{ex.StackTrace}", 
                "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (AppHost != null)
        {
            await AppHost.StopAsync();
            AppHost.Dispose();
        }
        base.OnExit(e);
    }
}
