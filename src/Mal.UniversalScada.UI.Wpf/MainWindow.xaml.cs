using System.Windows;
using Mal.UniversalScada.UI.Wpf.ViewModels;

namespace Mal.UniversalScada.UI.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        // 注意：InitializeAsync 已在 App.xaml.cs 中 Show() 之前提前调用，
        // 此处不再重复调用，避免 StartPollingEngine / _scheduler.StartAsync 被执行两次
        // 导致孤儿任务无法在关闭时被正确取消。

        Closing += (s, e) =>
        {
            _vm.Dispose();
        };
    }
}