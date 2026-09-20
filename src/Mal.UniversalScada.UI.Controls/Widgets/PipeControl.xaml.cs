using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.Widgets;

public partial class PipeControl : UserControl
{
    private Storyboard? _storyboard;
    private PipeWidgetViewModel? _currentPipeVm;
    private PipeProps? _currentPipeProps;

    public PipeControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        HookViewModel(e.NewValue);
        StartFlowAnimation();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HookViewModel(DataContext);
        StartFlowAnimation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnhookViewModel();
        _storyboard?.Stop();
    }

    private void HookViewModel(object? dc)
    {
        UnhookViewModel();

        if (dc is PipeWidgetViewModel pipeVm)
        {
            _currentPipeVm = pipeVm;
            _currentPipeProps = pipeVm.Props;
            _currentPipeProps.PropertyChanged += OnPropsPropertyChanged;
            _currentPipeVm.PropertyChanged += OnVmPropertyChanged;
        }
        else if (dc is WidgetViewModel wvm && wvm.PipeProps != null)
        {
            _currentPipeProps = wvm.PipeProps;
            _currentPipeProps.PropertyChanged += OnPropsPropertyChanged;
            wvm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void UnhookViewModel()
    {
        if (_currentPipeProps != null)
        {
            _currentPipeProps.PropertyChanged -= OnPropsPropertyChanged;
            _currentPipeProps = null;
        }
        if (_currentPipeVm != null)
        {
            _currentPipeVm.PropertyChanged -= OnVmPropertyChanged;
            _currentPipeVm = null;
        }
    }

    private void OnPropsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PipeProps.FlowDirection) or
                               nameof(PipeProps.FlowSpeed) or
                               nameof(PipeProps.IsFlowing) or
                               nameof(PipeProps.Orientation))
        {
            Dispatcher.InvokeAsync(StartFlowAnimation);
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PipeWidgetViewModel.FlowDirection) or
                               nameof(PipeWidgetViewModel.FlowSpeed) or
                               nameof(PipeWidgetViewModel.IsFlowing) or
                               nameof(PipeWidgetViewModel.Orientation))
        {
            Dispatcher.InvokeAsync(StartFlowAnimation);
        }
    }

    public void StartFlowAnimation()
    {
        _storyboard?.Stop();
        _storyboard = null;

        var pipeProps = _currentPipeProps ?? (_currentPipeVm?.Props) ?? (DataContext as PipeWidgetViewModel)?.Props ?? (DataContext as WidgetViewModel)?.PipeProps;
        if (pipeProps == null) return;

        if (!pipeProps.IsFlowing)
        {
            return; // 暂停流动
        }

        double durationSec = pipeProps.FlowSpeed > 0.05 ? pipeProps.FlowSpeed : 1.5;
        bool isReverse = string.Equals(pipeProps.FlowDirection, "Reverse", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(pipeProps.FlowDirection, "反向", StringComparison.OrdinalIgnoreCase);

        // 虚线位移方向：
        // StrokeDashArray="8,6", 周期 14 单位
        // 沿 X 轴 / Y 轴由小到大为正向（左->右，上->下）：StrokeDashOffset 从 0 到 -28
        // 沿 X 轴 / Y 轴由大到小为反向（右->左，下->上）：StrokeDashOffset 从 0 到 +28
        double toValue = isReverse ? 28.0 : -28.0;

        var hAnim = new DoubleAnimation
        {
            From = 0.0,
            To = toValue,
            Duration = new Duration(TimeSpan.FromSeconds(durationSec)),
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(hAnim, HorizontalFlowLine);
        Storyboard.SetTargetProperty(hAnim, new PropertyPath(Shape.StrokeDashOffsetProperty));

        var vAnim = new DoubleAnimation
        {
            From = 0.0,
            To = toValue,
            Duration = new Duration(TimeSpan.FromSeconds(durationSec)),
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(vAnim, VerticalFlowLine);
        Storyboard.SetTargetProperty(vAnim, new PropertyPath(Shape.StrokeDashOffsetProperty));

        _storyboard = new Storyboard();
        _storyboard.Children.Add(hAnim);
        _storyboard.Children.Add(vAnim);
        _storyboard.Begin();
    }
}
