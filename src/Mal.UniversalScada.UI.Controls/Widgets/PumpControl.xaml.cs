using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.Widgets;

public partial class PumpControl : UserControl
{
    public static event Action<WidgetViewModel>? ToggleRequested;
    private RotateTransform? _impellerRotateTransform;
    private DoubleAnimation? _rotationAnimation;
    private PumpProps? _currentProps;

    public PumpControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HookProps(DataContext);
        UpdateAnimationState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnhookProps();
        GetRotateTransform()?.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        HookProps(e.NewValue);
        UpdateAnimationState();
    }

    private void HookProps(object? dc)
    {
        UnhookProps();
        if (dc is PumpWidgetViewModel pvm)
        {
            _currentProps = pvm.Props;
            _currentProps.PropertyChanged += OnPropsPropertyChanged;
        }
        else if (dc is WidgetViewModel wvm && wvm.PumpProps != null)
        {
            _currentProps = wvm.PumpProps;
            _currentProps.PropertyChanged += OnPropsPropertyChanged;
        }
    }

    private void UnhookProps()
    {
        if (_currentProps != null)
        {
            _currentProps.PropertyChanged -= OnPropsPropertyChanged;
            _currentProps = null;
        }
    }

    private void OnPropsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PumpProps.IsRunning) ||
            e.PropertyName == nameof(PumpProps.IsFault) ||
            e.PropertyName == nameof(PumpProps.RotationSpeedRpm))
        {
            UpdateAnimationState();
        }
    }

    private RotateTransform? GetRotateTransform()
    {
        if (_impellerRotateTransform != null) return _impellerRotateTransform;
        if (ImpellerHost?.RenderTransform is RotateTransform rt)
        {
            _impellerRotateTransform = rt;
        }
        else if (ImpellerHost != null)
        {
            _impellerRotateTransform = new RotateTransform(0);
            ImpellerHost.RenderTransform = _impellerRotateTransform;
        }
        return _impellerRotateTransform;
    }

    private void UpdateAnimationState()
    {
        var rt = GetRotateTransform();
        if (rt == null) return;

        bool shouldRun = _currentProps?.IsRunning == true && _currentProps?.IsFault != true;
        if (shouldRun)
        {
            double durationSec = 1.2;
            if (_currentProps != null && _currentProps.RotationSpeedRpm > 10.0)
            {
                // 根据实际设定 RPM 动态计算转速动画周期 (如 1450 RPM => 约 0.8s 视觉周期)
                durationSec = Math.Clamp(60.0 / _currentProps.RotationSpeedRpm * 20.0, 0.3, 2.5);
            }

            _rotationAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 360.0,
                Duration = new Duration(TimeSpan.FromSeconds(durationSec)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            rt.BeginAnimation(RotateTransform.AngleProperty, _rotationAnimation);
        }
        else
        {
            rt.BeginAnimation(RotateTransform.AngleProperty, null);
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WidgetViewModel vm && !vm.IsDesignMode)
        {
            if (vm is PumpWidgetViewModel pvm)
            {
                bool nextRun = !pvm.Props.IsRunning;
                pvm.Props.UpdateState(nextRun);
                vm.WriteValue = nextRun ? "1" : "0";
                ToggleRequested?.Invoke(vm);
            }
            e.Handled = true;
        }
    }
}
