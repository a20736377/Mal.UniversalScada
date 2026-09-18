using System;
using System.Windows;
using System.Windows.Controls;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.Widgets;

public partial class ControlButtonControl : UserControl
{
    public static event Action<WidgetViewModel>? ExecuteRequested;

    public ControlButtonControl()
    {
        InitializeComponent();
    }

    private void OnExecuteClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is WidgetViewModel vm)
        {
            ExecuteRequested?.Invoke(vm);
        }
    }
}
