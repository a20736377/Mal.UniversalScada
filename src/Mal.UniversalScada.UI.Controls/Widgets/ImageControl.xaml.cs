using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.Widgets;

public partial class ImageControl : UserControl
{
    private DispatcherTimer? _refreshTimer;
    private ImageProps? _currentProps;

    public ImageControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HookProps(DataContext);
        UpdateAutoRefreshState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnhookProps();
        StopRefreshTimer();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        HookProps(e.NewValue);
        UpdateAutoRefreshState();
    }

    private void HookProps(object? dc)
    {
        UnhookProps();
        if (dc is ImageWidgetViewModel ivm)
        {
            _currentProps = ivm.Props;
            _currentProps.PropertyChanged += OnPropsPropertyChanged;
        }
        else if (dc is WidgetViewModel wvm && wvm.ImageProps != null)
        {
            _currentProps = wvm.ImageProps;
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
        if (e.PropertyName == nameof(ImageProps.IsAutoRefresh) ||
            e.PropertyName == nameof(ImageProps.RefreshIntervalSec) ||
            e.PropertyName == nameof(ImageProps.ImagePath))
        {
            UpdateAutoRefreshState();
        }
    }

    private void UpdateAutoRefreshState()
    {
        if (_currentProps != null && _currentProps.IsAutoRefresh && _currentProps.RefreshIntervalSec > 0 &&
            !string.IsNullOrWhiteSpace(_currentProps.ImagePath))
        {
            if (_refreshTimer == null)
            {
                _refreshTimer = new DispatcherTimer();
                _refreshTimer.Tick += OnRefreshTimerTick;
            }

            int sec = Math.Clamp(_currentProps.RefreshIntervalSec, 1, 3600);
            _refreshTimer.Interval = TimeSpan.FromSeconds(sec);
            if (!_refreshTimer.IsEnabled)
            {
                _refreshTimer.Start();
            }
        }
        else
        {
            StopRefreshTimer();
        }
    }

    private void StopRefreshTimer()
    {
        if (_refreshTimer != null)
        {
            _refreshTimer.Stop();
            _refreshTimer = null;
        }
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        if (_currentProps == null || string.IsNullOrWhiteSpace(_currentProps.ImagePath))
            return;

        try
        {
            string path = _currentProps.ImagePath;
            string fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // 强制刷新缓存
                bitmap.EndInit();
                bitmap.Freeze();
                ImgDisplay.Source = bitmap;
            }
        }
        catch
        {
            // 忽略定时更新过程中的异常
        }
    }
}
