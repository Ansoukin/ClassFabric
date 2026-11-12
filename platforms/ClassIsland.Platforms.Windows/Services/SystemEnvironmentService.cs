using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ClassIsland.Platforms.Abstraction.Services;
using ClassIsland.Platform.Windows.Helpers;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClassIsland.Platform.Windows.Services;

/// <summary>
/// Windows 端系统环境调整实现：壁纸/主题走注册表 + SystemParametersInfo，
/// 音量走 MMDevice COM（手写 ComImport，无额外包），截图走 GDI+，窗口操作走 user32。
/// </summary>
public class SystemEnvironmentService : ISystemEnvironmentService
{
    /// <inheritdoc />
    public bool SetWallpaperImage(string imagePath, int fitStyle)
    {
        try
        {
            var (tileValue, styleValue) = fitStyle switch
            {
                0 => ("1", "1"),  // 平铺
                1 => ("0", "0"),  // 居中
                2 => ("0", "2"),  // 拉伸
                3 => ("0", "10"), // 填充
                4 => ("0", "6"),  // 适应
                5 => ("0", "22"), // 跨区
                _ => ("0", "10")  // 默认：填充
            };

            using (var desktopRegKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
            {
                if (desktopRegKey == null)
                {
                    return false;
                }

                desktopRegKey.SetValue("TileWallpaper", tileValue, RegistryValueKind.String);
                desktopRegKey.SetValue("WallpaperStyle", styleValue, RegistryValueKind.String);
            }

            return ApplyWallpaper(imagePath);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool SetWallpaperSolidColor(byte r, byte g, byte b)
    {
        try
        {
            using (var colorsRegKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors", true))
            {
                if (colorsRegKey == null)
                {
                    return false;
                }

                colorsRegKey.SetValue("Background", $"{r} {g} {b}", RegistryValueKind.String);
            }

            using (var desktopRegKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
            {
                if (desktopRegKey == null)
                {
                    return false;
                }

                desktopRegKey.SetValue("Wallpaper", string.Empty, RegistryValueKind.String);
                desktopRegKey.SetValue("TileWallpaper", "0", RegistryValueKind.String);
                desktopRegKey.SetValue("WallpaperStyle", "0", RegistryValueKind.String);
            }

            return ApplyWallpaper(string.Empty);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool SetSystemTheme(bool light)
    {
        try
        {
            using var registryKey =
                Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            registryKey.SetValue("AppsUseLightTheme", light ? 1 : 0, RegistryValueKind.DWord);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public AccentColorSnapshot CaptureAccentColor() => AccentColorHelper.Capture();

    /// <inheritdoc />
    public bool ApplyAccentColor(AccentColorRgb color) => AccentColorHelper.Apply(color);

    /// <inheritdoc />
    public bool RestoreAccentColor(AccentColorSnapshot snapshot) => AccentColorHelper.Restore(snapshot);

    /// <inheritdoc />
    public bool SetSystemVolume(float percent, bool? mute)
    {
        try
        {
            var endpoint = new MMDeviceEnumeratorWrapper()
                .GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
            var volume = endpoint.GetAudioEndpointVolume();
            volume.SetMasterVolumeLevelScalar(Math.Clamp(percent, 0, 100) / 100f, Guid.Empty);
            if (mute is { } muted)
            {
                volume.SetMute(muted, Guid.Empty);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool CaptureScreenToPng(string filePath)
    {
        try
        {
            // 虚拟屏幕（多屏全区域）。这几个 SM_* 常量在当前 CsWin32 版本里不随函数生成，直接手写声明与数值。
            const int SmXVirtualScreen = 76;
            const int SmYVirtualScreen = 77;
            const int SmCxVirtualScreen = 78;
            const int SmCyVirtualScreen = 79;
            var x = GetSystemMetrics(SmXVirtualScreen);
            var y = GetSystemMetrics(SmYVirtualScreen);
            var width = GetSystemMetrics(SmCxVirtualScreen);
            var height = GetSystemMetrics(SmCyVirtualScreen);
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            using var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(x, y, 0, 0, bitmap.Size);
            bitmap.Save(filePath, ImageFormat.Png);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    /// <inheritdoc />
    public bool OperateForegroundWindow(ForegroundWindowOperation operation)
    {
        var window = PInvoke.GetForegroundWindow();
        if (window == HWND.Null)
        {
            return false;
        }

        // 关闭走 WM_CLOSE 消息（等价点 X），其余是标准 ShowWindow 命令。
        switch (operation)
        {
            case ForegroundWindowOperation.Maximize:
                return PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_MAXIMIZE);
            case ForegroundWindowOperation.Minimize:
                return PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_MINIMIZE);
            case ForegroundWindowOperation.Restore:
                return PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_RESTORE);
            case ForegroundWindowOperation.Close:
                return PInvoke.PostMessage(window, 0x0010 /* WM_CLOSE */, default, default);
            default:
                return false;
        }
    }

    unsafe bool ApplyWallpaper(string wallpaperPath)
    {
        IntPtr uniPtr = IntPtr.Zero;
        try
        {
            uniPtr = Marshal.StringToHGlobalUni(wallpaperPath);
            var result = PInvoke.SystemParametersInfo(
                SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETDESKWALLPAPER,
                0,
                (void*)uniPtr,
                SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_UPDATEINIFILE |
                SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_SENDCHANGE);
            return result;
        }
        finally
        {
            if (uniPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(uniPtr);
            }
        }
    }
}

// ---------------- MMDevice COM Interop（手写声明，无 NuGet 包） ----------------

/// <summary>
/// COM 类 MMDeviceEnumerator 的 CLSID 包装。
/// </summary>
[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorCom
{
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(EDataFlow dataFlow, DeviceState stateMask, out IntPtr devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, CLSCTX clsCtx, IntPtr activationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
}

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig]
    int RegisterControlChangeNotify(IntPtr notify);

    [PreserveSig]
    int UnregisterControlChangeNotify(IntPtr notify);

    [PreserveSig]
    int GetChannelCount(out uint count);

    [PreserveSig]
    int SetMasterVolumeLevel(float levelDB, Guid eventContext);

    [PreserveSig]
    int SetMasterVolumeLevelScalar(float level, Guid eventContext);

    [PreserveSig]
    int GetMasterVolumeLevel(out float levelDB);

    [PreserveSig]
    int GetMasterVolumeLevelScalar(out float level);

    [PreserveSig]
    int SetChannelVolumeLevel(uint channel, float levelDB, Guid eventContext);

    [PreserveSig]
    int GetChannelVolumeLevel(uint channel, out float levelDB);

    [PreserveSig]
    int SetChannelVolumeLevelScalar(uint channel, float level, Guid eventContext);

    [PreserveSig]
    int GetChannelVolumeLevelScalar(uint channel, out float level);

    [PreserveSig]
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid eventContext);

    [PreserveSig]
    int GetMute(out bool mute);

    [PreserveSig]
    int GetVolumeStepInfo(out uint step, out uint stepCount);

    [PreserveSig]
    int VolumeStepUp(Guid eventContext);

    [PreserveSig]
    int VolumeStepDown(Guid eventContext);

    [PreserveSig]
    int QueryHardwareSupport(out uint hardwareSupportMask);

    [PreserveSig]
    int GetVolumeRange(out float volumeMinDB, out float volumeMaxDB, out float volumeStepDB);
}

internal enum EDataFlow
{
    eRender,
    eCapture,
    eAll
}

internal enum ERole
{
    eConsole,
    eMultimedia,
    eCommunications
}

[Flags]
internal enum DeviceState
{
    ACTIVE = 0x00000001,
    DISABLED = 0x00000002,
    NOT_PRESENT = 0x00000004,
    UNPLUGGED = 0x00000008,
    MASK_ALL = 0x0000000F
}

[Flags]
internal enum CLSCTX
{
    INPROC_SERVER = 0x1,
    INPROC_HANDLER = 0x2,
    LOCAL_SERVER = 0x4,
    REMOTE_SERVER = 0x10,
    NO_CODE_DOWNLOAD = 0x400,
    NO_CUSTOM_MARSHAL = 0x1000,
    ENABLE_CODE_DOWNLOAD = 0x2000,
    NO_FAILURE_LOG = 0x4000,
    DISABLE_AAA = 0x8000,
    ENABLE_AAA = 0x10000,
    FROM_DEFAULT_CONTEXT = 0x20000,
    ACTIVATE_64_BIT_SERVER = 0x80000,
    ENABLE_CLOAKING = 0x100000,
    APPCONTAINER = 0x400000,
    ACTIVATE_AAA_AS_IU = 0x800000,
    PS_DLL = unchecked((int)0x80000000)
}

internal class MMDeviceEnumeratorWrapper
{
    readonly IMMDeviceEnumerator _enumerator;

    public MMDeviceEnumeratorWrapper()
    {
        var type = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
        if (type == null)
        {
            throw new InvalidOperationException("无法获取 MMDeviceEnumerator 类型。");
        }

        var instance = Activator.CreateInstance(type);
        if (instance == null)
        {
            throw new InvalidOperationException("无法创建 MMDeviceEnumerator 实例。");
        }

        _enumerator = (IMMDeviceEnumerator)instance;
    }

    public IMMDevice GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role)
    {
        _enumerator.GetDefaultAudioEndpoint(dataFlow, role, out var device);
        return device;
    }
}

internal static class MMDeviceExtensions
{
    static readonly Guid IidIAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    public static IAudioEndpointVolume GetAudioEndpointVolume(this IMMDevice device)
    {
        var iid = IidIAudioEndpointVolume;
        device.Activate(ref iid, CLSCTX.INPROC_SERVER, IntPtr.Zero, out var result);
        return (IAudioEndpointVolume)result;
    }
}
