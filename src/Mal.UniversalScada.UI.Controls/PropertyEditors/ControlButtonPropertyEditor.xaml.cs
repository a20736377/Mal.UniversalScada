using System.Windows;
using System.Windows.Controls;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.PropertyEditors;

public partial class ControlButtonPropertyEditor : UserControl
{
    public ControlButtonPropertyEditor()
    {
        InitializeComponent();
    }

    private void OnSetWriteValue1Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ControlButtonWidgetViewModel vm)
        {
            vm.Props.WriteValue = "1";
        }
    }

    private void OnSetWriteValue0Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ControlButtonWidgetViewModel vm)
        {
            vm.Props.WriteValue = "0";
        }
    }
}
