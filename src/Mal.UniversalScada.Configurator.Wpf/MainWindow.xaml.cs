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
}