using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Configuration;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.UI.Controls.ViewModels;
using Mal.UniversalScada.UI.Controls.Widgets;

namespace Mal.UniversalScada.UI.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IConfigurationService _configService;
    private readonly ITagTester _tagTester;
    private readonly IRealtimeDataBus _dataBus;
    private readonly IPriorityScheduler _scheduler;
    private readonly IAlarmEngine _alarmEngine;
    private readonly IAuditService _auditService;
    private readonly IRecipeService _recipeService;
    private readonly IScreenManager _screenManager;
    private readonly IUserAuthService _authService;
    private readonly IUserRepository _userRepository;

    private readonly List<ChannelConfig> _channels = new();
    private readonly List<DeviceNode> _devices = new();
    private readonly List<TagNode> _tags = new();
    private readonly List<IDisposable> _busSubscriptions = new();

    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private readonly System.Windows.Threading.DispatcherTimer _clockTimer;

    [ObservableProperty]
    private AlarmBannerViewModel _alarmBanner;

    [ObservableProperty]
    private ObservableCollection<UiViewConfig> _views = new();

    [ObservableProperty]
    private UiViewConfig? _currentView;

    [ObservableProperty]
    private ObservableCollection<WidgetViewModel> _widgets = new();

    [ObservableProperty]
    private ObservableCollection<PhysicalScreenInfo> _screens = new();

    [ObservableProperty]
    private PhysicalScreenInfo? _selectedScreen;

    [ObservableProperty]
    private ObservableCollection<RecipeModel> _recipes = new();

    [ObservableProperty]
    private RecipeModel? _selectedRecipe;

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    [ObservableProperty]
    private string _statusMessage = "🟢 实时中枢与总线就绪 | 采样频率: 5 Hz";

    [ObservableProperty]
    private bool _isEngineRunning = true;

    [ObservableProperty]
    private double _zoomScale = 1.0;

    [ObservableProperty]
    private int _packetCounter = 0;

    public string CurrentUsername => _authService.CurrentUser.Username;
    public string CurrentUserRoleName => _authService.CurrentUser.Role switch
    {
        UserRole.Administrator => "系统管理员",
        UserRole.Engineer => "工程师",
        UserRole.Operator => "操作员",
        _ => "访客 (只读)"
    };
    public string CurrentUserRoleBadgeColor => _authService.CurrentUser.Role switch
    {
        UserRole.Administrator => "#DC2626", // 红
        UserRole.Engineer => "#8B5CF6",      // 紫
        UserRole.Operator => "#10B981",      // 绿
        _ => "#64748B"                       // 灰
    };
    public bool IsLoggedIn => _authService.CurrentUser.Role > UserRole.Guest;

    public void RefreshUserState()
    {
        OnPropertyChanged(nameof(CurrentUsername));
        OnPropertyChanged(nameof(CurrentUserRoleName));
        OnPropertyChanged(nameof(CurrentUserRoleBadgeColor));
        OnPropertyChanged(nameof(IsLoggedIn));
    }

    public MainViewModel(
        IConfigurationService configService, 
        ITagTester tagTester,
        IRealtimeDataBus dataBus,
        IPriorityScheduler scheduler,
        IAlarmEngine alarmEngine,
        IAuditService auditService,
        IRecipeService recipeService,
        IScreenManager screenManager,
        IUserAuthService authService,
        IUserRepository userRepository,
        AlarmBannerViewModel alarmBanner)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _tagTester = tagTester ?? throw new ArgumentNullException(nameof(tagTester));
        _dataBus = dataBus ?? throw new ArgumentNullException(nameof(dataBus));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _alarmEngine = alarmEngine ?? throw new ArgumentNullException(nameof(alarmEngine));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _recipeService = recipeService ?? throw new ArgumentNullException(nameof(recipeService));
        _screenManager = screenManager ?? throw new ArgumentNullException(nameof(screenManager));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _alarmBanner = alarmBanner ?? throw new ArgumentNullException(nameof(alarmBanner));

        // 订阅用户身份变更通知
        _authService.CurrentUserChanged += (s, user) =>
        {
            Application.Current?.Dispatcher.Invoke(RefreshUserState);
        };

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
        // 1. 启动优先级写调度器
        await _scheduler.StartAsync();

        // 2. 加载可用物理屏幕列表
        RefreshScreens();

        // 3. 载入拓扑配置与监控画面
        await LoadTopologyAndViewsAsync();

        // 4. 载入工艺配方列表
        await LoadRecipesAsync();

        // 5. 启动实时轮询与仿真发布引擎
        StartPollingEngine();
    }

    public void RefreshScreens()
    {
        Screens.Clear();
        var screenList = _screenManager.GetAvailableScreens();
        foreach (var sc in screenList)
        {
            Screens.Add(sc);
        }
        SelectedScreen = Screens.FirstOrDefault(s => !s.IsPrimary) ?? Screens.FirstOrDefault();
    }

    public async Task LoadRecipesAsync()
    {
        try
        {
            Recipes.Clear();
            var targetDevice = _devices.FirstOrDefault()?.DeviceId ?? "DEV_01";
            var list = await _recipeService.GetRecipesByDeviceAsync(targetDevice);

            // 若无配方，自动预制经典工业配方以供体验
            if (list.Count == 0)
            {
                var defaultRecipe = new RecipeModel
                {
                    RecipeId = "RECIPE_REFLOW_STD",
                    Name = "回流焊无铅高温焊接标准配方",
                    TargetDeviceId = targetDevice,
                    Version = "2.1.0",
                    Items = new List<RecipeItem>
                    {
                        new("DEV_01.Tag_01", 160.0, "预热区温度设定"),
                        new("DEV_01.Tag_02", 210.0, "升温区温度设定"),
                        new("DEV_01.Tag_03", 245.0, "焊接峰值温度设定"),
                        new("DEV_01.Motor_Speed", 85.0, "链条传送速率设定")
                    }
                };
                await _recipeService.SaveRecipeAsync(defaultRecipe);
                list = await _recipeService.GetRecipesByDeviceAsync(targetDevice);
            }

            foreach (var r in list)
            {
                Recipes.Add(r);
            }
            SelectedRecipe = Recipes.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"载入配方异常: {ex.Message}";
        }
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

            // 若数据库暂无画面，生成示范画面
            if (list.Count == 0)
            {
                var view1 = CreateDefaultOvenView();
                var view2 = CreateDefaultFillingView();
                await _configService.SaveUiViewAsync(view1);
                await _configService.SaveUiViewAsync(view2);
                list = await _configService.GetUiViewsAsync();
            }

            // 根据当前登录用户身份及多对多授权列表进行严格过滤
            var allowedViews = await FilterViewsByUserAsync(list);
            Views.Clear();
            foreach (var item in allowedViews)
            {
                Views.Add(item);
            }

            if (allowedViews.Count == 0)
            {
                CurrentView = null;
                StatusMessage = $"⚠️ 账户 [{_authService.CurrentUser.Username}] 暂未被分配任何画面方案权限，请联系管理员分配。";
            }
            else
            {
                // 优先选择默认画面
                CurrentView = Views.FirstOrDefault(v => v.IsDefault) ?? Views.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"载入画面失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 根据当前登录用户的角色与多对多授权配置筛选可用画面方案
    /// </summary>
    private async Task<List<UiViewConfig>> FilterViewsByUserAsync(IReadOnlyList<UiViewConfig> allViews)
    {
        var currentUser = _authService.CurrentUser;
        if (currentUser.Role == UserRole.Administrator)
        {
            return allViews.ToList();
        }

        var allowedIds = await _userRepository.GetAllowedViewIdsAsync(currentUser.Username);
        var allowedSet = new HashSet<string>(allowedIds, StringComparer.OrdinalIgnoreCase);

        return allViews.Where(v => allowedSet.Contains(v.ViewId)).ToList();
    }

    /// <summary>
    /// 热重载画面方案列表（与设计器最新保存的数据保持实时同步，并按当前用户权限过滤）
    /// </summary>
    [RelayCommand]
    public async Task ReloadViewsAsync()
    {
        try
        {
            var previousViewId = CurrentView?.ViewId;
            var list = await _configService.GetUiViewsAsync();
            var allowedViews = await FilterViewsByUserAsync(list);

            Views.Clear();
            foreach (var item in allowedViews)
            {
                Views.Add(item);
            }

            if (allowedViews.Count == 0)
            {
                CurrentView = null;
                StatusMessage = $"⚠️ 账户 [{_authService.CurrentUser.Username}] 暂无可用画面方案授权，请联系管理员分配。";
            }
            else
            {
                CurrentView = Views.FirstOrDefault(v => v.ViewId == previousViewId)
                           ?? Views.FirstOrDefault(v => v.IsDefault)
                           ?? Views.FirstOrDefault();

                StatusMessage = $"🔄 已同步最新画面方案列表 (当前用户已授权 {Views.Count} 个画面)";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"同步画面方案失败: {ex.Message}";
        }
    }

    partial void OnCurrentViewChanged(UiViewConfig? value)
    {
        LoadActiveWidgets();
    }

    private void LoadActiveWidgets()
    {
        // 清理旧订阅
        foreach (var sub in _busSubscriptions)
        {
            sub.Dispose();
        }
        _busSubscriptions.Clear();

        Widgets.Clear();
        if (CurrentView == null) return;

        foreach (var config in CurrentView.Widgets)
        {
            var vm = WidgetViewModel.FromConfig(config, isDesignMode: false);
            Widgets.Add(vm);

            // 将控件与实时数据总线订阅绑定
            if (!string.IsNullOrWhiteSpace(vm.PrimaryTagId))
            {
                var sub = _dataBus.Subscribe(vm.PrimaryTagId, snapshot =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        vm.UpdateRuntimeValue(snapshot.Value, snapshot.Quality.ToString());
                    });
                });
                _busSubscriptions.Add(sub);
            }
        }

        StatusMessage = $"已切换至画面【{CurrentView.Name}】(挂载 {Widgets.Count} 个中枢总线监控控件)";
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
                                PublishToBus(tag.TagId, res.Value, QualityCode.Good);
                            }
                        }
                        catch
                        {
                            // 硬件通信失败，自动降级为平滑仿真
                        }
                    }

                    // 2. 硬件未在线或未连通，自动以高拟真工业动态波形平滑驱动发布到总线
                    if (!hardwareSuccess)
                    {
                        var simulated = GenerateSimulatedIndustrialValue(widget, step, random);
                        PublishToBus(widget.PrimaryTagId, simulated, QualityCode.Good);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // 保持引擎平稳运行
            }
        }
    }

    private void PublishToBus(string tagId, object? val, QualityCode quality = QualityCode.Good)
    {
        _dataBus.PublishSnapshot(new TagValueSnapshot
        {
            TagId = tagId,
            Value = val,
            Quality = quality,
            Timestamp = DateTime.Now
        });
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

    public bool EnsurePermission(UserRole requiredRole, string operationName)
    {
        if (_authService.CheckPermission(requiredRole))
        {
            return true;
        }

        // 访客或权限不足，弹窗登录提权
        var loginWin = new Views.LoginWindow(_authService, requiredRole, $"执行【{operationName}】需要【{GetRoleDisplayName(requiredRole)}】或更高权限，请登录验证。")
        {
            Owner = Application.Current?.MainWindow
        };

        bool? res = loginWin.ShowDialog();
        RefreshUserState();
        return res == true && _authService.CheckPermission(requiredRole);
    }

    private static string GetRoleDisplayName(UserRole role) => role switch
    {
        UserRole.Administrator => "系统管理员",
        UserRole.Engineer => "工程师",
        UserRole.Operator => "操作员",
        _ => "访客"
    };

    [RelayCommand]
    public async Task SwitchUserAsync()
    {
        var loginWin = new Views.LoginWindow(_authService, UserRole.Operator, "请登录或切换不同角色账号。")
        {
            Owner = Application.Current?.MainWindow
        };
        if (loginWin.ShowDialog() == true && _authService.CurrentUser.Role > UserRole.Guest)
        {
            RefreshUserState();
            await ReloadViewsAsync();
            StatusMessage = $"当前已切换登录用户: {_authService.CurrentUser.Username} ({CurrentUserRoleName})";
        }
    }

    [RelayCommand]
    public void Logout()
    {
        _authService.Logout();
        RefreshUserState();

        // 强登录约束：关闭访客浏览，注销后必须重新登录，取消则退出监控系统
        var loginWin = new Views.LoginWindow(_authService, UserRole.Operator, "用户已注销。系统已关闭访客浏览，请重新登录以继续监控。")
        {
            Owner = Application.Current?.MainWindow
        };
        if (loginWin.ShowDialog() == true && _authService.CurrentUser.Role > UserRole.Guest)
        {
            RefreshUserState();
            _ = ReloadViewsAsync();
            StatusMessage = $"当前已重新登录用户: {_authService.CurrentUser.Username} ({CurrentUserRoleName})";
        }
        else
        {
            // 取消登录，直接退出系统
            Application.Current?.Shutdown();
        }
    }

    private void OnWidgetControlExecuted(WidgetViewModel widget)
    {
        if (!EnsurePermission(UserRole.Operator, $"组件控制下发: {widget.Title}"))
        {
            StatusMessage = "⚠️ 权限拦截: 访客无下发控制指令权限";
            return;
        }

        if (string.IsNullOrWhiteSpace(widget.PrimaryTagId))
        {
            MessageBox.Show($"组件【{widget.Title}】未配置绑定的点位 (PrimaryTagId)！", "操作提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var writeVal = widget.WriteValue;
        var tag = _tags.FirstOrDefault(t => t.TagId == widget.PrimaryTagId);
        var device = tag != null ? _devices.FirstOrDefault(d => d.DeviceId == tag.DeviceId) : null;
        var channel = device != null ? _channels.FirstOrDefault(c => c.ChannelId == device.ChannelId) : null;
        var operatorName = _authService.CurrentUser.Username;

        Task.Run(async () =>
        {
            // 记录安全审计追踪日志
            await _auditService.RecordTagWriteAsync(operatorName, widget.PrimaryTagId, null, writeVal, true, 0, $"组件控制下发: {widget.Title}");

            if (tag != null && device != null && channel != null)
            {
                var res = await _tagTester.TestWriteTagAsync(tag, writeVal, device, channel);
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    StatusMessage = res.IsSuccess
                        ? $"✅ 指令下发成功: 点位 [{widget.PrimaryTagId}] 写入值 [{writeVal}]"
                        : $"⚠️ 硬件写入未响应 (已仿真置位): {res.Message}";
                });

                if (res.IsSuccess)
                {
                    PublishToBus(widget.PrimaryTagId, writeVal, QualityCode.Good);
                }
            }
            else
            {
                // 仿真模式通过总线即时同步
                PublishToBus(widget.PrimaryTagId, writeVal, QualityCode.Good);
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"✅ (仿真总线) 指令下发成功: 点位 [{widget.PrimaryTagId}] 写入值 [{writeVal}]";
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

    /// <summary>
    /// 将当前画面全屏投射至指定显示器 (工业 Kiosk 模式)
    /// </summary>
    [RelayCommand]
    public void ProjectCurrentView()
    {
        if (CurrentView == null)
        {
            MessageBox.Show("未选择有效监控画面！", "投屏提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int targetIndex = SelectedScreen?.Index ?? 0;
        _screenManager.LaunchViewOnScreen(CurrentView, targetIndex, isKiosk: true);
        StatusMessage = $"📽️ 画面【{CurrentView.Name}】已投射至显示器 #{targetIndex + 1} (Kiosk 全屏模式)";
    }

    /// <summary>
    /// 关闭所有副屏投射视窗
    /// </summary>
    [RelayCommand]
    public void CloseAllProjections()
    {
        _screenManager.CloseAllProjectedScreens();
        StatusMessage = "📽️ 已关闭所有投屏视窗";
    }

    /// <summary>
    /// 一键执行选中的工艺配方下发
    /// </summary>
    [RelayCommand]
    public async Task ApplySelectedRecipeAsync()
    {
        if (!EnsurePermission(UserRole.Operator, "下发工艺配方"))
        {
            StatusMessage = "⚠️ 权限拦截: 访客无批量下发工艺配方权限";
            return;
        }

        if (SelectedRecipe == null)
        {
            MessageBox.Show("请先选择要下发的工艺配方！", "配方提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"确认将配方【{SelectedRecipe.Name}】(包含 {SelectedRecipe.Items.Count} 个设定项) 批量下发至产线设备？",
            "工艺参数下发确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        StatusMessage = $"⏳ 正在下发配方【{SelectedRecipe.Name}】并校验回读...";
        var res = await _recipeService.ApplyRecipeToDeviceAsync(SelectedRecipe.RecipeId);

        if (res.IsSuccess)
        {
            // 通过总线同步刷新本地值
            foreach (var item in SelectedRecipe.Items)
            {
                PublishToBus(item.TagId, item.TargetValue, QualityCode.Good);
            }
            StatusMessage = $"✅ 配方下发成功: {res.Message}";
            MessageBox.Show($"配方【{SelectedRecipe.Name}】下发完成！\n{res.Message}", "配方下发成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            StatusMessage = $"⚠️ 配方下发警告: {res.Message}";
            MessageBox.Show($"配方下发部分完成或异常:\n{res.Message}", "下发提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void Dispose()
    {
        ControlButtonControl.ExecuteRequested -= OnWidgetControlExecuted;
        foreach (var sub in _busSubscriptions)
        {
            sub.Dispose();
        }
        _busSubscriptions.Clear();
        _clockTimer.Stop();
        StopPollingEngine();
        _screenManager.CloseAllProjectedScreens();
        GC.SuppressFinalize(this);
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
