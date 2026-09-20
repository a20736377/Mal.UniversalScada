using System.Collections.Generic;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 物理显示器信息模型
/// </summary>
public class PhysicalScreenInfo
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public double BoundsX { get; set; }
    public double BoundsY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsPrimary { get; set; }
    public string DisplayText => $"屏幕 {Index + 1} ({Width}x{Height}) {(IsPrimary ? "[主显示器]" : "[扩展副屏]")}";
}

/// <summary>
/// 多物理显示器自动识别与工业 Kiosk 全屏防误触独立视窗投射管理接口。
/// </summary>
public interface IScreenManager
{
    /// <summary>
    /// 获取当前主机连接的所有物理显示器列表
    /// </summary>
    IReadOnlyList<PhysicalScreenInfo> GetAvailableScreens();

    /// <summary>
    /// 将指定组态监控画面投射至指定显示器
    /// </summary>
    /// <param name="view">目标画面组态</param>
    /// <param name="screenIndex">目标物理屏幕索引</param>
    /// <param name="isKiosk">是否启用 Kiosk 全屏无边框防误触模式</param>
    void LaunchViewOnScreen(UiViewConfig view, int screenIndex, bool isKiosk = true);

    /// <summary>
    /// 关闭所有已投射的独立视窗
    /// </summary>
    void CloseAllProjectedScreens();
}
