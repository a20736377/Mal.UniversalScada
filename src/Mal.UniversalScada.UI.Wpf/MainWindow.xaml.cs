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

        Loaded += async (s, e) =>
        {
            await _vm.InitializeAsync();
        };

        Closing += (s, e) =>
        {
            _vm.Dispose();
        };
    }
}