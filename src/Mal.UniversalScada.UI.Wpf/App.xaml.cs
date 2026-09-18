using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mal.UniversalScada.Core.Configuration;
using Mal.UniversalScada.Drivers.CustomSerial;
using Mal.UniversalScada.Drivers.Modbus;
using Mal.UniversalScada.Drivers.Siemens;
using Mal.UniversalScada.Storage.Sqlite;
using Mal.UniversalScada.UI.Wpf.ViewModels;

namespace Mal.UniversalScada.UI.Wpf;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
                    // 1. 组态底层数据库仓储 (SQLite)
                    services.AddSqliteConfigStorage("Data Source=scada_config.db");

                    // 2. 核心服务 (通道、驱动工厂、点位读写测试器)
                    services.AddScadaConfigurationCore();
                    services.AddModbusDriver();
                    services.AddSiemensS7Driver();
                    services.AddCustomSerialDriver();

                    // 3. ViewModel 与主窗体
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await AppHost.StartAsync();

            var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"SCADA 监控系统启动失败: {ex.Message}\n\n{ex.StackTrace}", 
                "启动异常", MessageBoxButton.OK, MessageBoxImage.Error);
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
