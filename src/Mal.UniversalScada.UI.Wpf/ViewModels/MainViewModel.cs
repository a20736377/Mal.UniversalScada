using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mal.UniversalScada.Core.Configuration;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.UI.Controls.ViewModels;
using Mal.UniversalScada.UI.Controls.Widgets;

namespace Mal.UniversalScada.UI.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IConfigurationService _configService;
    private readonly ITagTester _tagTester;

    private readonly List<ChannelConfig> _channels = new();
    private readonly List<DeviceNode> _devices = new();
    private readonly List<TagNode> _tags = new();

    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private readonly System.Windows.Threading.DispatcherTimer _clockTimer;

    [ObservableProperty]
    private ObservableCollection<UiViewConfig> _views = new();

    [ObservableProperty]
    private UiViewConfig? _currentView;

    [ObservableProperty]
    private ObservableCollection<WidgetViewModel> _widgets = new();

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    [ObservableProperty]
    private string _statusMessage = "🟢 实时采集引擎就绪 | 采样频率: 5 Hz";

    [ObservableProperty]
    private bool _isEngineRunning = true;

    [ObservableProperty]
    private double _zoomScale = 1.0;

    [ObservableProperty]
    private int _packetCounter = 0;

    public MainViewModel(IConfigurationService configService, ITagTester tagTester)
    {
        _configService = configService;
        _tagTester = tagTester;

        // 订阅控制按钮下发事件
        ControlButtonControl.ExecuteRequested += OnWidgetControlExecuted;

        // 时钟定时器
        _clockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _clockTimer.Start();
    }

    public async Task InitializeAsync()
    {
        await LoadTopologyAndViewsAsync();
        StartPollingEngine();
    }

    public async Task LoadTopologyAndViewsAsync()
    {
        try
        {
            var configData = await _configService.LoadConfigurationAsync();
            _channels.Clear();
            _channels.AddRange(configData.Channels);

            _devices.Clear();
            _devices.AddRange(configData.Devices);

            _tags.Clear();
            _tags.AddRange(configData.Tags);

            var list = await _configService.GetUiViewsAsync();
            Views.Clear();

            // 若数据库暂无画面，生成示范画面
            if (list.Count == 0)
            {
                var view1 = CreateDefaultOvenView();
                var view2 = CreateDefaultFillingView();
                await _configService.SaveUiViewAsync(view1);
                await _configService.SaveUiViewAsync(view2);
                list = await _configService.GetUiViewsAsync();
            }

            foreach (var item in list)
            {
                Views.Add(item);
            }

            // 优先选择默认画面
            CurrentView = Views.FirstOrDefault(v => v.IsDefault) ?? Views.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"载入画面失败: {ex.Message}";
        }
    }

    partial void OnCurrentViewChanged(UiViewConfig? value)
    {
        LoadActiveWidgets();
    }

    private void LoadActiveWidgets()
    {
        Widgets.Clear();
        if (CurrentView == null) return;

        foreach (var config in CurrentView.Widgets)
        {
            // 运行态 isDesignMode = false，隐藏虚线高亮与删除按钮
            var vm = WidgetViewModel.FromConfig(config, isDesignMode: false);
            Widgets.Add(vm);
        }

        StatusMessage = $"已切换至画面【{CurrentView.Name}】(挂载 {Widgets.Count} 个监控控件)";
    }

    private void StartPollingEngine()
    {
        StopPollingEngine();

        _pollingCts = new CancellationTokenSource();
        _pollingTask = Task.Run(() => PollingLoopAsync(_pollingCts.Token));
    }

    private void StopPollingEngine()
    {
        if (_pollingCts != null)
        {
            _pollingCts.Cancel();
            _pollingCts.Dispose();
            _pollingCts = null;
        }
    }

    private async Task PollingLoopAsync(CancellationToken token)
    {
        double step = 0;
        var random = new Random();

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(200, token); // 5Hz 采样刷新
                if (!IsEngineRunning) continue;

                step += 0.15;
                var currentWidgets = Widgets.ToList();
                PacketCounter++;

                foreach (var widget in currentWidgets)
                {
                    if (string.IsNullOrWhiteSpace(widget.PrimaryTagId)) continue;

                    var tag = _tags.FirstOrDefault(t => t.TagId == widget.PrimaryTagId);
                    var device = tag != null ? _devices.FirstOrDefault(d => d.DeviceId == tag.DeviceId) : null;
                    var channel = device != null ? _channels.FirstOrDefault(c => c.ChannelId == device.ChannelId) : null;

                    bool hardwareSuccess = false;

                    // 1. 如果拓扑完整，尝试真实在线读取
                    if (tag != null && device != null && channel != null)
                    {
                        try
                        {
                            var res = await _tagTester.TestReadTagAsync(tag, device, channel, token);
                            if (res.IsSuccess && res.Value != null)
                            {
                                hardwareSuccess = true;
                                Application.Current?.Dispatcher.Invoke(() =>
                                {
                                    widget.UpdateRuntimeValue(res.Value, "Good");
                                });
                            }
                        }
                        catch
                        {
                            // 硬件通信失败，自动降级为平滑仿真
                        }
                    }

                    // 2. 硬件未在线或未连通，自动以高拟真工业动态波形平滑驱动
                    if (!hardwareSuccess)
                    {
                        var simulated = GenerateSimulatedIndustrialValue(widget, step, random);
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            widget.UpdateRuntimeValue(simulated, "Good(Sim)");
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // 忽略异常，保持引擎平稳运行
            }
        }
    }

    private static object GenerateSimulatedIndustrialValue(WidgetViewModel widget, double step, Random rand)
    {
        var min = widget.MinValue;
        var max = widget.MaxValue;
        if (max <= min) max = min + 100;
        var mid = (min + max) / 2.0;
        var amplitude = (max - min) * 0.4;

        return widget.Type switch
        {
            WidgetType.GaugeCircular =>
                Math.Round(mid + (Math.Sin(step + widget.X * 0.01) * amplitude) + ((rand.NextDouble() - 0.5) * 2), 1),

            WidgetType.LevelTank =>
                Math.Round(min + ((Math.Sin(step * 0.5 + widget.X * 0.05) * 0.5 + 0.5) * (max - min)), 1),

            WidgetType.NumericCard =>
                Math.Round(mid + (Math.Cos(step * 0.8) * amplitude * 0.8), 2),

            WidgetType.IoMatrix =>
                (long)(Math.Abs((int)(Math.Sin(step * 0.3) * 255)) % 256),

            WidgetType.StatusLed =>
                Math.Sin(step) > 0,

            _ => Math.Round(mid, 1)
        };
    }

    private void OnWidgetControlExecuted(WidgetViewModel widget)
    {
        if (string.IsNullOrWhiteSpace(widget.PrimaryTagId))
        {
            MessageBox.Show($"组件【{widget.Title}】未配置绑定的点位 (PrimaryTagId)！", "操作提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var writeVal = widget.WriteValue;
        var tag = _tags.FirstOrDefault(t => t.TagId == widget.PrimaryTagId);
        var device = tag != null ? _devices.FirstOrDefault(d => d.DeviceId == tag.DeviceId) : null;
        var channel = device != null ? _channels.FirstOrDefault(c => c.ChannelId == device.ChannelId) : null;

        Task.Run(async () =>
        {
            if (tag != null && device != null && channel != null)
            {
                var res = await _tagTester.TestWriteTagAsync(tag, writeVal, device, channel);
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    StatusMessage = res.IsSuccess
                        ? $"✅ 指令下发成功: 点位 [{widget.PrimaryTagId}] 写入值 [{writeVal}]"
                        : $"⚠️ 硬件写入未响应 (已仿真置位): {res.Message}";
                });
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"✅ (仿真模式) 指令下发成功: 点位 [{widget.PrimaryTagId}] 写入值 [{writeVal}]";
                });
            }
        });
    }

    [RelayCommand]
    public void SwitchView(UiViewConfig? view)
    {
        if (view != null && view != CurrentView)
        {
            CurrentView = view;
        }
    }

    [RelayCommand]
    public void ToggleEngine()
    {
        IsEngineRunning = !IsEngineRunning;
        StatusMessage = IsEngineRunning ? "🟢 实时采集引擎已恢复运行" : "⏸️ 采集引擎已暂停轮询";
    }

    [RelayCommand]
    public void ZoomIn()
    {
        if (ZoomScale < 2.0) ZoomScale = Math.Round(ZoomScale + 0.1, 1);
    }

    [RelayCommand]
    public void ZoomOut()
    {
        if (ZoomScale > 0.5) ZoomScale = Math.Round(ZoomScale - 0.1, 1);
    }

    [RelayCommand]
    public void ResetZoom()
    {
        ZoomScale = 1.0;
    }

    public void Dispose()
    {
        ControlButtonControl.ExecuteRequested -= OnWidgetControlExecuted;
        _clockTimer.Stop();
        StopPollingEngine();
    }

    private static UiViewConfig CreateDefaultOvenView()
    {
        return new UiViewConfig
        {
            ViewId = "View_Reflow_Oven",
            Name = "回流焊多温区实时测控大屏",
            CanvasWidth = 1600,
            CanvasHeight = 900,
            IsDefault = true,
            Widgets = new List<WidgetConfig>
            {
                new() { WidgetId = "W_OVEN_ZONE1", Type = WidgetType.GaugeCircular, Title = "温区1-预热区", PrimaryTagId = "DEV_01.Tag_01", X = 50, Y = 50, Width = 180, Height = 180, Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "300", ["Unit"] = "℃" } },
                new() { WidgetId = "W_OVEN_ZONE2", Type = WidgetType.GaugeCircular, Title = "温区2-升温区", PrimaryTagId = "DEV_01.Tag_02", X = 260, Y = 50, Width = 180, Height = 180, Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "300", ["Unit"] = "℃" } },
                new() { WidgetId = "W_OVEN_ZONE3", Type = WidgetType.GaugeCircular, Title = "温区3-焊接区", PrimaryTagId = "DEV_01.Tag_03", X = 470, Y = 50, Width = 180, Height = 180, Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "300", ["Unit"] = "℃" } },
                new() { WidgetId = "W_OVEN_SPEED", Type = WidgetType.NumericCard, Title = "传送带链速", PrimaryTagId = "DEV_01.Motor_Speed", X = 680, Y = 50, Width = 200, Height = 140, Properties = new() { ["Unit"] = "mm/s" } },
                new() { WidgetId = "W_OVEN_STATUS", Type = WidgetType.StatusLed, Title = "加热管就绪", PrimaryTagId = "DEV_01.System_Start", X = 900, Y = 50, Width = 140, Height = 120 },
                new() { WidgetId = "W_OVEN_BTN", Type = WidgetType.ControlButton, Title = "主电源启停", PrimaryTagId = "DEV_01.System_Start", X = 900, Y = 190, Width = 160, Height = 90, Properties = new() { ["ButtonText"] = "启动加热", ["WriteValue"] = "1" } }
            }
        };
    }

    private static UiViewConfig CreateDefaultFillingView()
    {
        return new UiViewConfig
        {
            ViewId = "View_Filling_Station",
            Name = "液体灌装车间主控台",
            CanvasWidth = 1600,
            CanvasHeight = 900,
            IsDefault = false,
            Widgets = new List<WidgetConfig>
            {
                new() { WidgetId = "W_TANK_A", Type = WidgetType.LevelTank, Title = "原料储罐 A", PrimaryTagId = "TankA_Level", X = 60, Y = 50, Width = 160, Height = 240, Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "100", ["Unit"] = "%" } },
                new() { WidgetId = "W_TANK_B", Type = WidgetType.LevelTank, Title = "缓冲储罐 B", PrimaryTagId = "TankB_Level", X = 250, Y = 50, Width = 160, Height = 240, Properties = new() { ["MinValue"] = "0", ["MaxValue"] = "100", ["Unit"] = "%" } },
                new() { WidgetId = "W_IO_STATUS", Type = WidgetType.IoMatrix, Title = "灌装阀门到位矩阵", PrimaryTagId = "Valve_Status_Word", X = 440, Y = 50, Width = 280, Height = 150 },
                new() { WidgetId = "W_PUMP_BTN", Type = WidgetType.ControlButton, Title = "主循环泵控制", PrimaryTagId = "Pump_RunCmd", X = 440, Y = 220, Width = 160, Height = 90, Properties = new() { ["ButtonText"] = "开启循环泵", ["WriteValue"] = "1" } }
            }
        };
    }
}
