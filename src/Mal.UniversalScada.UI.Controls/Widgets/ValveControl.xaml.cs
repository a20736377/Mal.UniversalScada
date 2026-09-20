using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.Widgets;

public partial class ValveControl : UserControl
{
    public static event Action<WidgetViewModel>? ToggleRequested;

    public ValveControl()
    {
        InitializeComponent();
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WidgetViewModel vm && !vm.IsDesignMode)
        {
            if (vm is ValveWidgetViewModel vvm)
            {
                // 运行态点击反转阀门开闭状态并下发控制
                bool nextState = !vvm.Props.IsOpen;
                vvm.Props.UpdateState(nextState);
                vm.WriteValue = nextState ? "1" : "0";
                ToggleRequested?.Invoke(vm);
            }
            e.Handled = true;
        }
    }
}
