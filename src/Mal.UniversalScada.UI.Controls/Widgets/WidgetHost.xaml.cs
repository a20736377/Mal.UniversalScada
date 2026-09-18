using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.Widgets;

public partial class WidgetHost : UserControl
{
    public static event Action<WidgetViewModel>? WidgetSelected;
    public static event Action<WidgetViewModel>? WidgetDeleteRequested;

    private bool _isDragging;
    private Point _dragStartMouse;
    private Point _dragStartPos;

    public WidgetHost()
    {
        InitializeComponent();

        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        PreviewMouseMove += OnPreviewMouseMove;
        PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        LostMouseCapture += (s, e) => _isDragging = false;
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not WidgetViewModel vm || !vm.IsDesignMode) return;

        // 如果点击的是右上角删除按钮，则不进入拖拽
        if (e.OriginalSource is DependencyObject dep && FindParent<Button>(dep) == DeleteButton)
        {
            return;
        }

        WidgetSelected?.Invoke(vm);

        var parentCanvas = FindParent<Canvas>(this);
        if (parentCanvas != null)
        {
            _isDragging = true;
            _dragStartMouse = e.GetPosition(parentCanvas);
            _dragStartPos = new Point(vm.X, vm.Y);
            CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || DataContext is not WidgetViewModel vm || !vm.IsDesignMode) return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            return;
        }

        var parentCanvas = FindParent<Canvas>(this);
        if (parentCanvas == null) return;

        var currentMouse = e.GetPosition(parentCanvas);
        var deltaX = currentMouse.X - _dragStartMouse.X;
        var deltaY = currentMouse.Y - _dragStartMouse.Y;

        // 磁吸网格 (以 10px 为栅格单位)
        var newX = Math.Max(0, Math.Round((_dragStartPos.X + deltaX) / 10.0) * 10.0);
        var newY = Math.Max(0, Math.Round((_dragStartPos.Y + deltaY) / 10.0) * 10.0);

        vm.X = newX;
        vm.Y = newY;
        e.Handled = true;
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void OnDeleteWidgetClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is WidgetViewModel vm)
        {
            WidgetDeleteRequested?.Invoke(vm);
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
}
