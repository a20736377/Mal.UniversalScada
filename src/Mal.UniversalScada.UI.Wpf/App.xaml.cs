using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Configuration;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.Drivers.CustomSerial;
using Mal.UniversalScada.Drivers.Modbus;
using Mal.UniversalScada.Drivers.Siemens;
using Mal.UniversalScada.Storage.Sqlite;
using Mal.UniversalScada.UI.Wpf.ViewModels;
using Mal.UniversalScada.UI.Wpf.Views;

namespace Mal.UniversalScada.UI.Wpf;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 核心设置：设置显式退出模式，防止登录窗口关闭时直接退出整个应用程序
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"SCADA 运行监控发生未处理异常: {args.Exception.Message}\n\n{args.Exception.StackTrace}", 
                "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // 1. 组态底层数据库仓储 (统一共享 SQLite 数据库)
                    services.AddSqliteConfigStorage();

                    // 2. 核心服务 (通道、驱动工厂、点位读写测试器)
                    services.AddScadaConfigurationCore();
                    services.AddModbusDriver();
                    services.AddSiemensS7Driver();
                    services.AddCustomSerialDriver();

                    // 3. 高性能中枢与工业核心服务 (阶段二 & 阶段三)
                    services.AddSingleton<Mal.UniversalScada.Core.Abstractions.IRealtimeDataBus, Mal.UniversalScada.Core.Services.RealtimeDataBus>();
                    services.AddSingleton<Mal.UniversalScada.Core.Abstractions.IPriorityScheduler, Mal.UniversalScada.Core.Services.PriorityScheduler>();
                    services.AddSingleton<Mal.UniversalScada.Core.Abstractions.IAlarmEngine, Mal.UniversalScada.Core.Services.AlarmEngine>();
                    services.AddSingleton<Mal.UniversalScada.Core.Abstractions.IAuditService, Mal.UniversalScada.Core.Services.AuditService>();
                    services.AddSingleton<Mal.UniversalScada.Core.Abstractions.IRecipeService, Mal.UniversalScada.Core.Services.RecipeService>();
                    services.AddSingleton<Mal.UniversalScada.Core.Abstractions.IScreenManager, Mal.UniversalScada.UI.Wpf.Services.ScreenManager>();
                    services.AddSingleton<Mal.UniversalScada.Core.Abstractions.IUserAuthService, Mal.UniversalScada.Core.Services.UserAuthService>();

                    // 4. ViewModel 与主窗体
                    services.AddSingleton<Mal.UniversalScada.UI.Controls.ViewModels.AlarmBannerViewModel>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                    services.AddTransient<LoginWindow>();
                })
                .Build();

            await AppHost.StartAsync();

            // 步骤 1：前置强制登录验证（禁止访客免密浏览）
            var authService = AppHost.Services.GetRequiredService<IUserAuthService>();
            var loginWindow = new LoginWindow(authService, UserRole.Operator, "请输入系统账号与密码登录以访问 SCADA 监控系统。系统已关闭访客浏览，未经授权无法查看画面。")
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            bool? loginSuccess = loginWindow.ShowDialog();

            if (loginSuccess == true && authService.CurrentUser.Role > UserRole.Guest)
            {
                // 核心关键：登录成功后，在展示监控主界面之前，先异步初始化并获取该用户的可用可视化画面方案
                var mainViewModel = AppHost.Services.GetRequiredService<MainViewModel>();
                await mainViewModel.InitializeAsync();

                // 步骤 2：验证通过且画面方案已加载就绪，显示监控主界面
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
            MessageBox.Show($"SCADA 监控系统启动失败: {ex.Message}\n\n{ex.StackTrace}", 
                "启动异常", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            AppHost?.Dispose();
            AppHost = null;
        }
        catch { }

        base.OnExit(e);

        // 核心保障：工控监控系统涉及物理底层驱动及后台通信总线，主窗体关闭后立即终结进程，杜绝任何假死与僵尸进程残留
        Environment.Exit(0);
    }
}
