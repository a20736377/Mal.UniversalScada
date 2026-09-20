using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mal.UniversalScada.Configurator.Wpf.Views;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Configuration;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Configurator.Wpf.ViewModels;

/// <summary>
/// 画面授权多对多关系项 ViewModel
/// </summary>
public partial class ViewPermissionItemViewModel : ObservableObject
{
    public string ViewId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int WidgetCount { get; set; }
    public double CanvasWidth { get; set; }
    public double CanvasHeight { get; set; }

    [ObservableProperty]
    private bool _isAllowed;
}

/// <summary>
/// SCADA 用户与多对多画面方案授权管理视图模型
/// </summary>
public partial class UserManagementViewModel : ObservableObject
{
    private readonly IUserRepository _userRepository;
    private readonly IConfigurationService _configService;

    public ObservableCollection<UserInfo> Users { get; } = new();
    public ObservableCollection<ViewPermissionItemViewModel> AvailableViews { get; } = new();

    [ObservableProperty]
    private UserInfo? _selectedUser;

    [ObservableProperty]
    private bool _isAdminSelected;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private bool _isLoading;

    public UserManagementViewModel(IUserRepository userRepository, IConfigurationService configService)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadDataAsync();
        StatusMessage = "🔄 用户与权限数据已从数据库刷新";
    }

    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            var prevUsername = SelectedUser?.Username;

            // 1. 载入所有用户
            var users = await _userRepository.GetAllUsersAsync();
            Users.Clear();
            foreach (var u in users)
            {
                Users.Add(u);
            }

            // 2. 载入所有画面方案
            var views = await _configService.GetUiViewsAsync();
            AvailableViews.Clear();
            foreach (var v in views)
            {
                AvailableViews.Add(new ViewPermissionItemViewModel
                {
                    ViewId = v.ViewId,
                    Name = v.Name,
                    IsDefault = v.IsDefault,
                    WidgetCount = v.Widgets?.Count ?? 0,
                    CanvasWidth = v.CanvasWidth,
                    CanvasHeight = v.CanvasHeight,
                    IsAllowed = false
                });
            }

            // 3. 恢复选中的用户或默认选中第一个
            SelectedUser = Users.FirstOrDefault(u => u.Username == prevUsername) ?? Users.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载用户与权限失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    async partial void OnSelectedUserChanged(UserInfo? value)
    {
        if (value == null)
        {
            IsAdminSelected = false;
            foreach (var v in AvailableViews)
            {
                v.IsAllowed = false;
            }
            return;
        }

        IsAdminSelected = value.Role == UserRole.Administrator;

        try
        {
            var allowedIds = await _userRepository.GetAllowedViewIdsAsync(value.Username);
            var allowedSet = new HashSet<string>(allowedIds, StringComparer.OrdinalIgnoreCase);

            foreach (var viewItem in AvailableViews)
            {
                // 如果是管理员，视觉上保持全选或根据库中授权记录
                viewItem.IsAllowed = IsAdminSelected || allowedSet.Contains(viewItem.ViewId);
            }

            StatusMessage = $"当前选中用户: {value.DisplayName} ({value.Username}) | 角色: {GetRoleName(value.Role)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载用户权限失败: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CreateUserAsync()
    {
        var dialog = new UserEditDialog
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.ResultUser != null)
        {
            try
            {
                var newUser = dialog.ResultUser;
                await _userRepository.SaveUserAsync(newUser, dialog.ResultPassword);

                // 重新加载用户列表
                await LoadDataAsync();
                SelectedUser = Users.FirstOrDefault(u => u.Username == newUser.Username);

                StatusMessage = $"✅ 成功创建新用户: {newUser.Username} ({newUser.DisplayName})";
                MessageBox.Show($"用户 [{newUser.Username}] 创建成功！", "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"创建用户失败: {ex.Message}";
                MessageBox.Show($"创建用户失败: {ex.Message}", "系统异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public async Task EditUserAsync()
    {
        if (SelectedUser == null)
        {
            MessageBox.Show("请先在左侧选择要编辑的用户！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new UserEditDialog(SelectedUser)
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.ResultUser != null)
        {
            try
            {
                var updatedUser = dialog.ResultUser;
                await _userRepository.SaveUserAsync(updatedUser, dialog.ResultPassword);

                var prevUsername = updatedUser.Username;
                await LoadDataAsync();
                SelectedUser = Users.FirstOrDefault(u => u.Username == prevUsername);

                StatusMessage = $"✅ 成功更新用户: {updatedUser.Username}";
                MessageBox.Show($"用户 [{updatedUser.Username}] 信息更新成功！", "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"更新用户失败: {ex.Message}";
                MessageBox.Show($"更新用户失败: {ex.Message}", "系统异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public async Task DeleteUserAsync()
    {
        if (SelectedUser == null)
        {
            MessageBox.Show("请先选择要删除的用户！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedUser.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("系统默认超级管理员账户 [admin] 受到受保护机制保护，禁止删除！", "安全拦截", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"确定要彻底删除用户【{SelectedUser.Username} ({SelectedUser.DisplayName})】吗？\n该操作将同时清理该用户的所有画面方案授权关系！",
            "确认删除用户",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var targetUsername = SelectedUser.Username;
            var success = await _userRepository.DeleteUserAsync(targetUsername);
            if (success)
            {
                await LoadDataAsync();
                StatusMessage = $"🗑️ 已成功删除用户 [{targetUsername}]";
            }
            else
            {
                MessageBox.Show($"删除用户 [{targetUsername}] 失败，可能用户不存在或被系统保护。", "操作失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除用户异常: {ex.Message}";
            MessageBox.Show($"删除用户异常: {ex.Message}", "系统异常", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task ToggleUserEnabledAsync(UserInfo? user)
    {
        user ??= SelectedUser;
        if (user == null) return;

        if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase) && user.IsEnabled)
        {
            MessageBox.Show("不能禁用默认超级管理员账户 admin！", "安全拦截", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        user.IsEnabled = !user.IsEnabled;
        await _userRepository.SaveUserAsync(user);
        await LoadDataAsync();
        SelectedUser = Users.FirstOrDefault(u => u.Username == user.Username);
        StatusMessage = $"用户 [{user.Username}] 状态已设置为: {(user.IsEnabled ? "启用" : "禁用")}";
    }

    [RelayCommand]
    public async Task SavePermissionsAsync()
    {
        if (SelectedUser == null)
        {
            MessageBox.Show("请先选择要授权的目标用户！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedUser.Role == UserRole.Administrator)
        {
            MessageBox.Show("系统超级管理员默认拥有全部画面方案的最高访问权限，无需额外限制授权。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var checkedIds = AvailableViews.Where(v => v.IsAllowed).Select(v => v.ViewId).ToList();
            await _userRepository.SetAllowedViewIdsAsync(SelectedUser.Username, checkedIds);

            StatusMessage = $"✅ 已成功保存用户 [{SelectedUser.Username}] 的画面方案授权！共授权 {checkedIds.Count} 个画面";
            MessageBox.Show($"用户 [{SelectedUser.Username}] 的画面方案权限保存成功！\n共分配 {checkedIds.Count} 个画面方案。", "授权成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存画面方案授权失败: {ex.Message}";
            MessageBox.Show($"保存画面方案授权失败: {ex.Message}", "保存异常", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void SelectAllViews()
    {
        if (IsAdminSelected) return;
        foreach (var v in AvailableViews)
        {
            v.IsAllowed = true;
        }
    }

    [RelayCommand]
    public void UnselectAllViews()
    {
        if (IsAdminSelected) return;
        foreach (var v in AvailableViews)
        {
            v.IsAllowed = false;
        }
    }

    private static string GetRoleName(UserRole role) => role switch
    {
        UserRole.Administrator => "系统管理员",
        UserRole.Engineer => "工程师",
        UserRole.Operator => "操作员",
        _ => "访客"
    };
}
