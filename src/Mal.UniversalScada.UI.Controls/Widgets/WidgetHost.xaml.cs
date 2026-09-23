using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.Widgets;

public partial class WidgetHost : UserControl
{
    public static event Action<WidgetViewModel>? WidgetSelected;
    public static event Action<WidgetViewModel, bool>? WidgetSelectionRequested;
    public static event Action<WidgetViewModel>? WidgetDeleteRequested;

    private bool _isDragging;
    private Point _dragStartMouse;
    private Point _dragStartPos;
    private readonly List<(WidgetViewModel Vm, Point StartPos)> _multiDragPositions = new();

    public WidgetHost()
    {
        InitializeComponent();

        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        PreviewMouseMove += OnPreviewMouseMove;
        PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        LostMouseCapture += (s, e) =>
        {
            _isDragging = false;
            _multiDragPositions.Clear();
        };
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not WidgetViewModel vm || !vm.IsDesignMode) return;

        // 如果点击的是右上角删除按钮或尺寸调节 Thumb，则不进入整体移动拖拽
        if (e.OriginalSource is DependencyObject dep)
        {
            if (FindParent<Button>(dep) == DeleteButton) return;
            if (FindParent<Thumb>(dep) != null)
            {
                // 点击的是尺寸调节手柄，保持并激活当前组件选中，但不拦截事件，让 Thumb 自行处理拖拽缩放
                bool isCtrlThumb = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                WidgetSelectionRequested?.Invoke(vm, isCtrlThumb);
                WidgetSelected?.Invoke(vm);
                return;
            }
        }

        bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        WidgetSelectionRequested?.Invoke(vm, isCtrl);
        WidgetSelected?.Invoke(vm);

        var parentCanvas = FindParent<Canvas>(this);
        if (parentCanvas != null)
        {
            _isDragging = true;
            _dragStartMouse = e.GetPosition(parentCanvas);
            _dragStartPos = new Point(vm.X, vm.Y);

            _multiDragPositions.Clear();
            var itemsControl = FindParent<ItemsControl>(parentCanvas);
            if (itemsControl?.ItemsSource is IEnumerable<WidgetViewModel> allVms)
            {
                var selected = allVms.Where(w => w.IsSelected).ToList();
                if (selected.Contains(vm) && selected.Count > 1)
                {
                    foreach (var item in selected)
                    {
                        _multiDragPositions.Add((item, new Point(item.X, item.Y)));
                    }
                }
            }

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
            _multiDragPositions.Clear();
            ReleaseMouseCapture();
            return;
        }

        var parentCanvas = FindParent<Canvas>(this);
        if (parentCanvas == null) return;

        var currentMouse = e.GetPosition(parentCanvas);
        var deltaX = currentMouse.X - _dragStartMouse.X;
        var deltaY = currentMouse.Y - _dragStartMouse.Y;

        if (_multiDragPositions.Count > 1)
        {
            var snapDx = Math.Round(deltaX / 10.0) * 10.0;
            var snapDy = Math.Round(deltaY / 10.0) * 10.0;

            foreach (var (item, startPos) in _multiDragPositions)
            {
                item.X = Math.Max(0, Math.Round((startPos.X + snapDx) / 10.0) * 10.0);
                item.Y = Math.Max(0, Math.Round((startPos.Y + snapDy) / 10.0) * 10.0);
            }
        }
        else
        {
            // 磁吸网格 (以 10px 为栅格单位)
            var newX = Math.Max(0, Math.Round((_dragStartPos.X + deltaX) / 10.0) * 10.0);
            var newY = Math.Max(0, Math.Round((_dragStartPos.Y + deltaY) / 10.0) * 10.0);

            vm.X = newX;
            vm.Y = newY;
        }

        e.Handled = true;
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _multiDragPositions.Clear();
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

    private void OnResizeThumbDragStarted(object sender, DragStartedEventArgs e)
    {
        if (DataContext is not WidgetViewModel vm || !vm.IsDesignMode) return;
        UpdateSizeBadgeText(vm);
        SizeBadge.Visibility = Visibility.Visible;
    }

    private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is not WidgetViewModel vm || !vm.IsDesignMode) return;
        if (sender is not Thumb thumb || thumb.Tag is not string direction) return;

        const double minWidth = 40.0;
        const double minHeight = 30.0;
        const double snap = 5.0; // 5 像素栅格微吸附

        switch (direction)
        {
            case "SE": // 东南角：调整宽和高
            {
                double nw = Math.Max(minWidth, Math.Round((vm.Width + e.HorizontalChange) / snap) * snap);
                double nh = Math.Max(minHeight, Math.Round((vm.Height + e.VerticalChange) / snap) * snap);
                vm.Width = nw;
                vm.Height = nh;
                break;
            }
            case "E": // 东侧：调整宽度
            {
                double nw = Math.Max(minWidth, Math.Round((vm.Width + e.HorizontalChange) / snap) * snap);
                vm.Width = nw;
                break;
            }
            case "S": // 南侧：调整高度
            {
                double nh = Math.Max(minHeight, Math.Round((vm.Height + e.VerticalChange) / snap) * snap);
                vm.Height = nh;
                break;
            }
            case "W": // 西侧：改变宽度并反向平移 X
            {
                double delta = Math.Round(e.HorizontalChange / snap) * snap;
                double nw = vm.Width - delta;
                if (nw >= minWidth)
                {
                    vm.Width = nw;
                    vm.X = Math.Max(0, vm.X + delta);
                }
                break;
            }
            case "N": // 北侧：改变高度并反向平移 Y
            {
                double delta = Math.Round(e.VerticalChange / snap) * snap;
                double nh = vm.Height - delta;
                if (nh >= minHeight)
                {
                    vm.Height = nh;
                    vm.Y = Math.Max(0, vm.Y + delta);
                }
                break;
            }
            case "NW": // 西北角：改变 X, Y, 宽, 高
            {
                double dx = Math.Round(e.HorizontalChange / snap) * snap;
                double dy = Math.Round(e.VerticalChange / snap) * snap;
                double nw = vm.Width - dx;
                double nh = vm.Height - dy;
                if (nw >= minWidth)
                {
                    vm.Width = nw;
                    vm.X = Math.Max(0, vm.X + dx);
                }
                if (nh >= minHeight)
                {
                    vm.Height = nh;
                    vm.Y = Math.Max(0, vm.Y + dy);
                }
                break;
            }
            case "NE": // 东北角：改变 Y, 宽, 高
            {
                double dy = Math.Round(e.VerticalChange / snap) * snap;
                double nw = Math.Max(minWidth, Math.Round((vm.Width + e.HorizontalChange) / snap) * snap);
                double nh = vm.Height - dy;
                vm.Width = nw;
                if (nh >= minHeight)
                {
                    vm.Height = nh;
                    vm.Y = Math.Max(0, vm.Y + dy);
                }
                break;
            }
            case "SW": // 西南角：改变 X, 宽, 高
            {
                double dx = Math.Round(e.HorizontalChange / snap) * snap;
                double nw = vm.Width - dx;
                double nh = Math.Max(minHeight, Math.Round((vm.Height + e.VerticalChange) / snap) * snap);
                if (nw >= minWidth)
                {
                    vm.Width = nw;
                    vm.X = Math.Max(0, vm.X + dx);
                }
                vm.Height = nh;
                break;
            }
        }

        UpdateSizeBadgeText(vm);
    }

    private void OnResizeThumbDragCompleted(object sender, DragCompletedEventArgs e)
    {
        SizeBadge.Visibility = Visibility.Collapsed;
    }

    private void UpdateSizeBadgeText(WidgetViewModel vm)
    {
        SizeBadgeText.Text = $"{Math.Round(vm.Width)} × {Math.Round(vm.Height)} px";
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
