using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mal.UniversalScada.Configurator.Wpf.ViewModels;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.UI.Controls.ViewModels;
using Mal.UniversalScada.UI.Controls.Widgets;

namespace Mal.UniversalScada.Configurator.Wpf.Views;

public partial class UiDesignerView : UserControl
{
    private Point _toolboxStartPos;

    public UiDesignerView()
    {
        InitializeComponent();

        Focusable = true;
        PreviewKeyDown += OnDesignerPreviewKeyDown;

        Loaded += (_, _) =>
        {
            if (DataContext is UiDesignerViewModel vm)
            {
                _ = vm.ReloadAvailableTagsAsync();
            }
        };

        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true && DataContext is UiDesignerViewModel vm)
            {
                _ = vm.ReloadAvailableTagsAsync();
            }
        };
    }

    private void OnDesignerPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not UiDesignerViewModel vm) return;

        // 如果光标在输入框中，不拦截文本编辑按键
        if (e.OriginalSource is TextBox || e.OriginalSource is PasswordBox) return;

        bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        double step = isShift ? 10.0 : 1.0;

        if (isCtrl && e.Key == Key.Z)
        {
            if (vm.CanUndo) vm.UndoCommand.Execute(null);
            e.Handled = true;
        }
        else if (isCtrl && e.Key == Key.Y)
        {
            if (vm.CanRedo) vm.RedoCommand.Execute(null);
            e.Handled = true;
        }
        else if (isCtrl && isShift && e.Key == Key.G)
        {
            if (vm.CanUngroup) vm.UngroupSelectedWidgetsCommand.Execute(null);
            e.Handled = true;
        }
        else if (isCtrl && !isShift && e.Key == Key.G)
        {
            if (vm.CanGroup) vm.GroupSelectedWidgetsCommand.Execute(null);
            e.Handled = true;
        }
        else if (isCtrl && e.Key == Key.C)
        {
            vm.CopySelectionCommand.Execute(null);
            e.Handled = true;
        }
        else if (isCtrl && e.Key == Key.V)
        {
            vm.PasteSelectionCommand.Execute(null);
            e.Handled = true;
        }
        else if (isCtrl && e.Key == Key.A)
        {
            vm.SelectAllWidgetsCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            vm.DeleteSelectedWidgetsCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            vm.NudgeSelectedWidgets(-step, 0);
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            vm.NudgeSelectedWidgets(step, 0);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            vm.NudgeSelectedWidgets(0, -step);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            vm.NudgeSelectedWidgets(0, step);
            e.Handled = true;
        }
    }

    #region 工具箱拖拽与双击

    private void OnToolboxItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is ToolboxItemRecord record && DataContext is UiDesignerViewModel vm)
        {
            vm.AddWidgetCommand.Execute(record.Type);
        }
    }

    private void OnToolboxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _toolboxStartPos = e.GetPosition(null);
    }

    private void OnToolboxMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is ListBox lb && lb.SelectedItem is ToolboxItemRecord record)
        {
            var currentPos = e.GetPosition(null);
            var diff = _toolboxStartPos - currentPos;
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                DragDrop.DoDragDrop(lb, record.Type, DragDropEffects.Copy);
            }
        }
    }

    #endregion

    #region 画布拖放接收 (Drop)

    private void OnCanvasDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(WidgetType)))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void OnCanvasDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(WidgetType)) is WidgetType widgetType && DataContext is UiDesignerViewModel vm)
        {
            var targetElement = sender as IInputElement;
            var pos = e.GetPosition(targetElement);

            // 10px 磁吸栅格
            var snapX = Math.Max(0, Math.Round(pos.X / 10.0) * 10.0);
            var snapY = Math.Max(0, Math.Round(pos.Y / 10.0) * 10.0);

            vm.AddWidgetAtPosition(widgetType, snapX, snapY);
            e.Handled = true;
        }
    }

    #endregion

    #region 画布空白点击与预览滑块

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        // 只有当真正点击空白画布区域（非 WidgetHost 内部）时，才取消组件选中
        if (e.OriginalSource is DependencyObject dep && FindParent<WidgetHost>(dep) == null && DataContext is UiDesignerViewModel vm)
        {
            foreach (var w in vm.Widgets)
            {
                w.IsSelected = false;
            }
            vm.SelectedWidget = null;
            vm.NotifySelectionChanged();
        }
    }

    private void OnPreviewSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DataContext is UiDesignerViewModel vm && vm.SelectedWidget != null)
        {
            vm.SelectedWidget.UpdateRuntimeValue(e.NewValue);
        }
    }

    private void OnSimulateValueOneClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is UiDesignerViewModel vm && vm.SelectedWidget != null)
        {
            vm.SelectedWidget.UpdateRuntimeValue(1);
        }
    }

    private void OnSimulateValueZeroClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is UiDesignerViewModel vm && vm.SelectedWidget != null)
        {
            vm.SelectedWidget.UpdateRuntimeValue(0);
        }
    }

    private void OnSimulateIoPatternClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hexStr &&
            DataContext is UiDesignerViewModel vm && vm.SelectedWidget != null)
        {
            string clean = hexStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hexStr[2..] : hexStr;
            if (long.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out var val))
            {
                vm.SelectedWidget.UpdateRuntimeValue(val);
            }
        }
    }

    private void OnSetButtonWriteValueClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string valStr &&
            DataContext is UiDesignerViewModel vm && vm.SelectedWidget is ControlButtonWidgetViewModel btnVm)
        {
            btnVm.WriteValue = valStr;
        }
    }

    private void OnSimulateDeviceOnlineClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is UiDesignerViewModel vm && vm.SelectedWidget != null)
        {
            vm.SelectedWidget.UpdateRuntimeValue("Online");
        }
    }

    private void OnSimulateDeviceTimeoutClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is UiDesignerViewModel vm && vm.SelectedWidget != null)
        {
            vm.SelectedWidget.UpdateRuntimeValue("Timeout");
        }
    }

    private void OnSimulateDeviceOfflineClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is UiDesignerViewModel vm && vm.SelectedWidget != null)
        {
            vm.SelectedWidget.UpdateRuntimeValue("Offline");
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T t) return t;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    #endregion
}
