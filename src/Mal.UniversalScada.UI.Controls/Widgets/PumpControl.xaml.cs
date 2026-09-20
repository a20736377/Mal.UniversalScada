using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.Widgets;

public partial class PumpControl : UserControl
{
    public static event Action<WidgetViewModel>? ToggleRequested;
    private Storyboard? _rotationStoryboard;
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
        _rotationStoryboard = TryFindResource("ImpellerRotationAnimation") as Storyboard;
        HookProps(DataContext);
        UpdateAnimationState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnhookProps();
        _rotationStoryboard?.Stop(this);
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
        if (e.PropertyName == nameof(PumpProps.IsRunning) || e.PropertyName == nameof(PumpProps.IsFault))
        {
            UpdateAnimationState();
        }
    }

    private void UpdateAnimationState()
    {
        if (_rotationStoryboard == null) return;

        bool shouldRun = _currentProps?.IsRunning == true && _currentProps?.IsFault != true;
        if (shouldRun)
        {
            _rotationStoryboard.Begin(this, isControllable: true);
        }
        else
        {
            _rotationStoryboard.Stop(this);
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
