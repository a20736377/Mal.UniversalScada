using System.Windows;
using Mal.UniversalScada.Configurator.Wpf.ViewModels;

namespace Mal.UniversalScada.Configurator.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel vm && e.NewValue is Models.TopologyTreeNode node)
        {
            vm.SelectedTreeNode = node;
        }
    }

    private void ChannelRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.DataGridRow row && row.Item is Core.Models.ChannelConfig ch && DataContext is MainViewModel vm)
        {
            vm.OpenChannelDetail(ch);
        }
    }

    private void DeviceRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.DataGridRow row && row.Item is Core.Models.DeviceNode dev && DataContext is MainViewModel vm)
        {
            vm.OpenDeviceDetail(dev);
        }
    }

    private void TagRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.DataGridRow row && row.Item is Core.Models.TagNode tag && DataContext is MainViewModel vm)
        {
            vm.OpenTagDetail(tag);
        }
    }
}