using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using Mal.UniversalScada.Core.Abstractions;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.UI.Wpf.Views;

namespace Mal.UniversalScada.UI.Wpf.Services;

/// <summary>
/// 多物理显示器自动枚举与工业 Kiosk 全屏防误触投射管理器实现。
/// </summary>
public class ScreenManager : IScreenManager
{
    private readonly IRealtimeDataBus _dataBus;
    private readonly List<RuntimeScreenWindow> _openWindows = new();

    public ScreenManager(IRealtimeDataBus dataBus)
    {
        _dataBus = dataBus ?? throw new ArgumentNullException(nameof(dataBus));
    }

    /// <inheritdoc />
    public IReadOnlyList<PhysicalScreenInfo> GetAvailableScreens()
    {
        var screens = new List<PhysicalScreenInfo>();

        try
        {
            int index = 0;
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var mi = new MonitorInfoEx();
                mi.cbSize = Marshal.SizeOf(typeof(MonitorInfoEx));

                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    double w = Math.Abs(mi.rcMonitor.Right - mi.rcMonitor.Left);
                    double h = Math.Abs(mi.rcMonitor.Bottom - mi.rcMonitor.Top);

                    screens.Add(new PhysicalScreenInfo
                    {
                        Index = index++,
                        DeviceName = mi.szDevice.TrimEnd('\0'),
                        BoundsX = mi.rcMonitor.Left,
                        BoundsY = mi.rcMonitor.Top,
                        Width = w,
                        Height = h,
                        IsPrimary = (mi.dwFlags & 1) != 0
                    });
                }
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // 如果 Win32 调用失败，安全降级使用 WPF SystemParameters 虚拟屏
            screens.Add(new PhysicalScreenInfo
            {
                Index = 0,
                DeviceName = "PrimaryDisplay",
                BoundsX = 0,
                BoundsY = 0,
                Width = SystemParameters.PrimaryScreenWidth,
                Height = SystemParameters.PrimaryScreenHeight,
                IsPrimary = true
            });
        }

        if (screens.Count == 0)
        {
            screens.Add(new PhysicalScreenInfo
            {
                Index = 0,
                DeviceName = "DefaultScreen",
                BoundsX = 0,
                BoundsY = 0,
                Width = 1920,
                Height = 1080,
                IsPrimary = true
            });
        }

        return screens;
    }

    /// <inheritdoc />
    public void LaunchViewOnScreen(UiViewConfig view, int screenIndex, bool isKiosk = true)
    {
        if (view == null) return;

        var screens = GetAvailableScreens();
        var targetScreen = (screenIndex >= 0 && screenIndex < screens.Count) ? screens[screenIndex] : screens[0];

        var window = new RuntimeScreenWindow();
        window.BindView(view, _dataBus);

        // 设置启动位置在目标屏幕
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = targetScreen.BoundsX;
        window.Top = targetScreen.BoundsY;
        window.Width = targetScreen.Width;
        window.Height = targetScreen.Height;

        if (isKiosk)
        {
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.WindowState = WindowState.Maximized;
            window.Topmost = true; // Kiosk 全屏保持顶层，防止误触切出
        }

        _openWindows.Add(window);
        window.Closed += (s, e) => _openWindows.Remove(window);
        window.Show();
    }

    /// <inheritdoc />
    public void CloseAllProjectedScreens()
    {
        var windowsToClose = _openWindows.ToArray();
        foreach (var w in windowsToClose)
        {
            try { w.Close(); } catch { }
        }
        _openWindows.Clear();
    }

    #region Win32 Monitor Interop

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfoEx
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    #endregion
}
