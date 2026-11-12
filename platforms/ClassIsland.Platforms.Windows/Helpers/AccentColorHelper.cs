using System.Runtime.InteropServices;
using ClassIsland.Platforms.Abstraction.Services;
using Microsoft.Win32;

namespace ClassIsland.Platform.Windows.Helpers;

/// <summary>
/// 读取与应用 Windows 系统强调色（Accent Color）。
/// </summary>
/// <remarks>
/// Windows 没有公开的「设置强调色」接口，这里叠加两条路径以做到「无需重启任何进程即可立即生效」：
/// 注册表写入保证持久化，uxtheme!SetUserColorPreference（序号 122）负责即时提交；
/// 未公开接口不可用时静默降级为「仅注册表 + 广播」。
/// 只切换强调色本身，不碰 ColorPrevalence，任务栏/标题栏配色保持用户原有设置。
/// </remarks>
public static class AccentColorHelper
{
    const string DwmKeyPath = @"Software\Microsoft\Windows\DWM";
    const string AccentKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    const string DesktopKeyPath = @"Control Panel\Desktop";

    const uint HwndBroadcast = 0xFFFF;
    const uint WmSettingChange = 0x001A;
    const uint SmtoAbortIfHung = 0x0002;

    // COLORREF 仅使用低 24 位，格式为 0x00BBGGRR。
    const uint ColorRefMask = 0x00FFFFFF;

    /// <summary>
    /// 参与快照/还原的注册表值。有意不包含 ColorPrevalence：本功能不改变强调色的应用范围。
    /// </summary>
    static readonly (string Path, string Name)[] TrackedValues =
    [
        (DwmKeyPath, "AccentColor"),
        (DwmKeyPath, "ColorizationColor"),
        (DwmKeyPath, "ColorizationAfterglow"),
        (AccentKeyPath, "AccentColorMenu"),
        (AccentKeyPath, "StartColorMenu"),
        (AccentKeyPath, "AccentPalette"),
        (DesktopKeyPath, "AutoColorization"),
    ];

    /// <summary>
    /// 捕获当前强调色相关状态，供之后还原。
    /// </summary>
    public static AccentColorSnapshot Capture()
    {
        var snapshot = new AccentColorSnapshot();

        if (TryGetImmersiveColorPreference(out var preference))
        {
            snapshot.Data["immersivePreference"] = (ulong)preference.color1 << 32 | preference.color2;
        }

        if (TryGetDwmColorizationParameters(out var colorization))
        {
            snapshot.Data["dwmColorization"] = colorization;
        }

        var registry = new Dictionary<string, object?>();
        foreach (var (path, name) in TrackedValues)
        {
            registry[$"{path}\\{name}"] = ReadEntry(path, name);
        }

        snapshot.Data["registry"] = registry;
        return snapshot;
    }

    /// <summary>
    /// 应用系统强调色。
    /// </summary>
    public static bool Apply(AccentColorRgb color)
    {
        try
        {
            // 记录应用前的窗口边框配色，稍后原样恢复（未公开接口会顺带改边框色）。
            var hadColorization = TryGetDwmColorizationParameters(out var originalColorization);

            // DWM\AccentColor 与 Explorer\Accent 的两个菜单色为 ABGR（0xAABBGGRR，Alpha 常为 FF）。
            var abgr = 0xFF000000u | ((uint)color.B << 16) | ((uint)color.G << 8) | color.R;

            WriteValue(DwmKeyPath, "AccentColor", unchecked((int)abgr), RegistryValueKind.DWord);
            WriteValue(AccentKeyPath, "AccentColorMenu", unchecked((int)abgr), RegistryValueKind.DWord);
            WriteValue(AccentKeyPath, "StartColorMenu", unchecked((int)abgr), RegistryValueKind.DWord);
            WriteValue(AccentKeyPath, "AccentPalette", BuildAccentPalette(color), RegistryValueKind.Binary);

            // 关闭「从背景自动选取强调色」，确保手动选择的颜色不被壁纸覆盖。
            WriteValue(DesktopKeyPath, "AutoColorization", 0, RegistryValueKind.DWord);

            TryApplyImmediate(color);

            if (hadColorization)
            {
                TrySetDwmColorizationParameters(originalColorization);
            }

            BroadcastSettingsChange("ImmersiveColorSet");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 把系统强调色还原为快照时的状态。
    /// </summary>
    public static bool Restore(AccentColorSnapshot snapshot)
    {
        try
        {
            if (snapshot.Data["registry"] is not Dictionary<string, object?> registry)
            {
                return false;
            }

            foreach (var (fullPath, entry) in registry)
            {
                var separator = fullPath.LastIndexOf('\\');
                if (separator <= 0)
                {
                    continue;
                }

                var path = fullPath[..separator];
                var name = fullPath[(separator + 1)..];
                using var key = Registry.CurrentUser.CreateSubKey(path, writable: true);
                if (key is null)
                {
                    continue;
                }

                if (entry is RegistryEntry value)
                {
                    key.SetValue(name, value.Data, value.Kind);
                }
                else
                {
                    key.DeleteValue(name, throwOnMissingValue: false);
                }
            }

            // 未公开接口把内存中的偏好一并还原，做到即时生效。
            if (snapshot.Data["immersivePreference"] is ulong packed &&
                TryGetImmersiveColorPreference(out _))
            {
                var preference = new ImmersiveColorPreference
                {
                    color1 = (uint)(packed >> 32),
                    color2 = (uint)packed
                };
                TrySetImmersiveColorPreference(preference);
            }

            BroadcastSettingsChange("ImmersiveColorSet");
            return true;
        }
        catch
        {
            return false;
        }
    }

    static void WriteValue(string path, string name, object value, RegistryValueKind kind)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path, writable: true);
        if (key is null)
        {
            throw new InvalidOperationException($"无法打开注册表项：HKCU\\{path}");
        }

        key.SetValue(name, value, kind);
    }

    static RegistryEntry? ReadEntry(string path, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        if (key is null)
        {
            return null;
        }

        try
        {
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return value is null ? null : new RegistryEntry(key.GetValueKind(name), value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 生成 AccentPalette（32 字节 = 8 × RGBA）。索引 0–2 更亮、3 为基准色、4–6 更暗，Alpha 为 0x00。
    /// </summary>
    static byte[] BuildAccentPalette(AccentColorRgb color)
    {
        var (hue, saturation, lightness) = RgbToHsl(color);

        double[] levels =
        [
            lightness + (1 - lightness) * 0.66,
            lightness + (1 - lightness) * 0.40,
            lightness + (1 - lightness) * 0.12,
            lightness,
            lightness - lightness * 0.10,
            lightness - lightness * 0.31,
            lightness - lightness * 0.51,
            lightness,
        ];

        var palette = new byte[32];
        for (var i = 0; i < levels.Length; i++)
        {
            var rgb = HslToRgb(hue, saturation, Math.Clamp(levels[i], 0, 1));
            var offset = i * 4;
            palette[offset] = rgb.R;
            palette[offset + 1] = rgb.G;
            palette[offset + 2] = rgb.B;
            palette[offset + 3] = 0x00;
        }

        return palette;
    }

    static (double Hue, double Saturation, double Lightness) RgbToHsl(AccentColorRgb color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2.0;
        var delta = max - min;

        if (delta < 1e-9)
        {
            return (0, 0, lightness);
        }

        var saturation = lightness > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

        double hue;
        if (Math.Abs(max - r) <= 1e-9)
        {
            hue = 60 * ((g - b) / delta);
        }
        else if (Math.Abs(max - g) <= 1e-9)
        {
            hue = 60 * ((b - r) / delta + 2);
        }
        else
        {
            hue = 60 * ((r - g) / delta + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        return (hue, saturation, lightness);
    }

    static AccentColorRgb HslToRgb(double hue, double saturation, double lightness)
    {
        if (saturation < 1e-9)
        {
            var gray = ToByte(lightness);
            return new AccentColorRgb(gray, gray, gray);
        }

        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var hp = hue / 60.0;
        var x = c * (1 - Math.Abs(hp % 2 - 1));
        var m = lightness - c / 2;

        double r1, g1, b1;
        switch ((int)Math.Floor(hp) % 6)
        {
            case 0: (r1, g1, b1) = (c, x, 0); break;
            case 1: (r1, g1, b1) = (x, c, 0); break;
            case 2: (r1, g1, b1) = (0, c, x); break;
            case 3: (r1, g1, b1) = (0, x, c); break;
            case 4: (r1, g1, b1) = (x, 0, c); break;
            default: (r1, g1, b1) = (c, 0, x); break;
        }

        return new AccentColorRgb(ToByte(r1 + m), ToByte(g1 + m), ToByte(b1 + m));
    }

    static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);

    static uint ToColorRef(AccentColorRgb color) =>
        color.R | ((uint)color.G << 8) | ((uint)color.B << 16);

    static bool TryApplyImmediate(AccentColorRgb color)
    {
        if (!TryGetImmersiveColorPreference(out var preference))
        {
            return false;
        }

        preference.color2 = ToColorRef(color) & ColorRefMask;
        return TrySetImmersiveColorPreference(preference);
    }

    static bool TryGetImmersiveColorPreference(out ImmersiveColorPreference preference)
    {
        preference = default;
        try
        {
            return GetUserColorPreference(ref preference, 0) == 0;
        }
        catch
        {
            return false;
        }
    }

    static bool TrySetImmersiveColorPreference(ImmersiveColorPreference preference)
    {
        try
        {
            return SetUserColorPreference(ref preference, 1) == 0;
        }
        catch
        {
            return false;
        }
    }

    static bool TryGetDwmColorizationParameters(out DwmColorizationParameters parameters)
    {
        parameters = default;
        try
        {
            return DwmGetColorizationParameters(out parameters) == 0;
        }
        catch
        {
            return false;
        }
    }

    static bool TrySetDwmColorizationParameters(DwmColorizationParameters parameters)
    {
        try
        {
            return DwmSetColorizationParameters(ref parameters, 0) == 0;
        }
        catch
        {
            return false;
        }
    }

    static void BroadcastSettingsChange(string area)
    {
        try
        {
            SendMessageTimeoutString((IntPtr)HwndBroadcast, WmSettingChange, IntPtr.Zero, area,
                SmtoAbortIfHung, 5000, out _);
        }
        catch
        {
            // 广播失败不影响已写入的注册表状态。
        }
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr SendMessageTimeoutString(IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
        uint flags, uint timeout, out IntPtr result);

    [DllImport("uxtheme.dll", EntryPoint = "GetUserColorPreference", ExactSpelling = true)]
    static extern int GetUserColorPreference(ref ImmersiveColorPreference preference, int forceReload);

    [DllImport("uxtheme.dll", EntryPoint = "#122", ExactSpelling = true)]
    static extern int SetUserColorPreference(ref ImmersiveColorPreference preference, int forceCommit);

    [DllImport("dwmapi.dll", EntryPoint = "#127", ExactSpelling = true)]
    static extern int DwmGetColorizationParameters(out DwmColorizationParameters parameters);

    [DllImport("dwmapi.dll", EntryPoint = "#131", ExactSpelling = true)]
    static extern int DwmSetColorizationParameters(ref DwmColorizationParameters parameters, uint unknown);

    [StructLayout(LayoutKind.Sequential)]
    struct ImmersiveColorPreference
    {
        public uint color1;
        public uint color2;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DwmColorizationParameters
    {
        public uint dwColor;
        public uint dwAfterglow;
        public uint dwColorBalance;
        public uint dwAfterglowBalance;
        public uint dwBlurBalance;
        public uint dwGlassReflectionIntensity;
        public uint dwOpaqueBlend;
    }

    readonly record struct RegistryEntry(RegistryValueKind Kind, object Data);
}
