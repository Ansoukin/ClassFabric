using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ClassIsland.Services;

/// <summary>
/// 托盘菜单实心化与圆角矫正。
/// Avalonia 12 的托盘菜单宿主（Win32 TrayIconImpl 内部的 TrayPopupRoot，一个普通 Window）以代码写死
/// TransparencyLevelHint=Transparent，该渲染路径下菜单底色被压低透明度，叠在壁纸上灰蒙蒙
/// （Avalonia 11 同路径为实心）；而窗口由平台内部代码创建，应用层样式改不动它的透明提示。
/// 本类轮询窗口集合，在菜单窗口出现后把透明提示改为 None 并铺主题实心底色，恢复旧版实心观感；
/// 同时用 Win32 圆角裁剪补偿窗口由此失去的圆角外形。
/// 升级 Avalonia 修复该渲染问题后，本类与 App 中的启动调用可整体删除。
/// </summary>
public static class PopupTransparencyCorrector
{
    private const string TrayPopupWindowNamePrefix = "AvaloniaTrayPopupRoot_";

    private static readonly Dictionary<Window, PixelSize> AppliedRegions = new();

    public static void Start()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background, (_, _) => Correct());
        timer.Start();
    }

    private static void Correct()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
        {
            return;
        }
        foreach (var window in lifetime.Windows)
        {
            if (window.Name?.StartsWith(TrayPopupWindowNamePrefix) != true || !window.IsVisible)
            {
                AppliedRegions.Remove(window);
                continue;
            }
            // 已修正的窗口（提示已为 None）持续跟随尺寸更新圆角裁剪；待修正的先转实心。
            if (window.TransparencyLevelHint.Contains(WindowTransparencyLevel.Transparent))
            {
                window.TransparencyLevelHint = [WindowTransparencyLevel.None];
                if (Application.Current.TryGetResource("AcrylicInAppFillColorDefaultBrush", window.ActualThemeVariant, out var value) &&
                    value is IBrush brush)
                {
                    window.Background = brush;
                }
            }
            ApplyRoundedRegion(window);
        }
    }

    private static void ApplyRoundedRegion(Window window)
    {
        var scale = window.RenderScaling;
        // 尺寸取物理像素，宽高各加一避免右/下边界因取整被裁掉一列像素。
        var size = new PixelSize((int)(window.Bounds.Width * scale) + 1, (int)(window.Bounds.Height * scale) + 1);
        if (AppliedRegions.TryGetValue(window, out var applied) && applied == size)
        {
            return;
        }
        var handle = window.TryGetPlatformHandle();
        if (handle == null)
        {
            return;
        }
        var radius = (int)(8 * scale);
        var region = CreateRoundRectRgn(0, 0, size.Width, size.Height, radius * 2, radius * 2);
        if (region == IntPtr.Zero)
        {
            return;
        }
        // SetWindowRgn 成功后 region 归系统所有，无需手动释放。
        SetWindowRgn(handle.Handle, region, true);
        AppliedRegions[window] = size;
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
}
