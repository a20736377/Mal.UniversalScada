using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Wpf.Views;

public partial class RuntimeScreenWindow : Window
{
    private readonly List<IDisposable> _subscriptions = new();
    private readonly ObservableCollection<WidgetViewModel> _widgets = new();

    public RuntimeScreenWindow()
    {
        InitializeComponent();
        WidgetsItemsControl.ItemsSource = _widgets;
    }

    public void BindView(UiViewConfig view, IRealtimeDataBus? dataBus)
    {
        TxtScreenTitle.Text = view.Name;
        DataContext = view;

        _widgets.Clear();
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();

        foreach (var cfg in view.Widgets)
        {
            var vm = WidgetViewModel.FromConfig(cfg, isDesignMode: false);
            _widgets.Add(vm);

            // 订阅总线
            if (dataBus != null && vm.PrimaryTagId > 0)
            {
                var sub = dataBus.Subscribe(vm.PrimaryTagId, snapshot =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        vm.UpdateRuntimeValue(snapshot.Value, snapshot.Quality.ToString());
                    });
                });
                _subscriptions.Add(sub);
            }
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
    }
}
