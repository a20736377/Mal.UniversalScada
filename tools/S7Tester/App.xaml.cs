using System.Windows;
using System.Windows.Threading;

namespace S7Tester;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        // 捕获 UI 线程未处理异常，弹窗显示，避免启动失败时静默无提示
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        // 捕获非 UI 线程未处理异常
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // 捕获未观察的任务异常
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"程序发生未处理异常：\n\n{e.Exception}",
            "S7Tester 错误",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"程序发生致命异常：\n\n{ex}",
                "S7Tester 致命错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        MessageBox.Show(
            $"后台任务异常：\n\n{e.Exception}",
            "S7Tester 后台错误",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.SetObserved();
    }
}
