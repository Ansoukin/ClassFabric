using System.ComponentModel;
using System.Runtime.InteropServices;
using ClassIsland.Platforms.Abstraction.Services;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ClassIsland.Platform.Windows.Services;

/// <summary>
/// Windows 端全局热键实现。
/// RegisterHotKey 需要一个有消息泵的窗口；插件版用 WinForms NativeWindow，主程序没有 WinForms，
/// 这里手写 message-only 窗口（HWND_MESSAGE 父级，不占任务栏不闪现）。窗口建在调用线程上，
/// Avalonia 的 UI 线程本来就在泵 Win32 消息，WM_HOTKEY 会自然派发进来。
/// </summary>
public class SystemHotkeyService : ISystemHotkeyService, IDisposable
{
    const int WmHotkey = 0x0312;
    const string WindowClassName = "ClassFabricSystemHotkeyWindow";

    readonly ILogger<SystemHotkeyService>? _logger;

    // WndProc 委托必须保活，被 GC 回收后窗口回调就是野指针；实例委托直接捕获 this，省去窗口属性表关联。
    readonly WndProcDelegate _wndProcDelegate;
    readonly object _lock = new();
    readonly Dictionary<int, HotkeyInfo> _registeredHotkeys = new();
    readonly Dictionary<int, HotkeyInfo> _suspendedHotkeys = new();
    int _nextHotkeyId = 0x9000;
    HWND _window;
    bool _disposed;

    public event EventHandler<SystemHotkeyEventArgs>? HotkeyPressed;

    delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public SystemHotkeyService(ILogger<SystemHotkeyService>? logger = null)
    {
        _logger = logger;
        _wndProcDelegate = WndProcHandler;
    }

    public int RegisterHotkey(int modifierKeys, uint virtualKey)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // 相同组合复用同一热键 ID：两条自动化配了同一个热键时只占一个系统注册位。
            foreach (var info in _registeredHotkeys.Values.Concat(_suspendedHotkeys.Values))
            {
                if (info.ModifierKeys == modifierKeys && info.VirtualKey == virtualKey)
                {
                    info.RefCount++;
                    _logger?.LogInformation("热键 {Hotkey} 已存在，复用 ID {Id}（引用计数 {Count}）",
                        GetHotkeyDisplay(modifierKeys, virtualKey), info.Id, info.RefCount);
                    return info.Id;
                }
            }

            EnsureWindow();

            var id = _nextHotkeyId++;
            if (!PInvoke.RegisterHotKey(_window, id, (HOT_KEY_MODIFIERS)(uint)modifierKeys, virtualKey))
            {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"注册热键 {GetHotkeyDisplay(modifierKeys, virtualKey)} 失败");
            }

            _registeredHotkeys[id] = new HotkeyInfo
            {
                Id = id,
                ModifierKeys = modifierKeys,
                VirtualKey = virtualKey,
                Display = GetHotkeyDisplay(modifierKeys, virtualKey),
                RefCount = 1
            };
            _logger?.LogInformation("热键 {Hotkey} 注册成功，ID {Id}",
                GetHotkeyDisplay(modifierKeys, virtualKey), id);
            return id;
        }
    }

    public void UnregisterHotkey(int hotkeyId)
    {
        lock (_lock)
        {
            if (!_registeredHotkeys.TryGetValue(hotkeyId, out var info))
            {
                return;
            }

            info.RefCount--;
            if (info.RefCount <= 0)
            {
                PInvoke.UnregisterHotKey(_window, hotkeyId);
                _registeredHotkeys.Remove(hotkeyId);
                _logger?.LogInformation("热键 {Hotkey} (ID {Id}) 已注销", info.Display, hotkeyId);
            }
        }
    }

    public bool IsHotkeyRegistered(int modifierKeys, uint virtualKey)
    {
        lock (_lock)
        {
            return _registeredHotkeys.Values.Any(i => i.ModifierKeys == modifierKeys && i.VirtualKey == virtualKey);
        }
    }

    public void SuspendAllHotkeys()
    {
        lock (_lock)
        {
            _suspendedHotkeys.Clear();
            foreach (var kvp in _registeredHotkeys.ToList())
            {
                PInvoke.UnregisterHotKey(_window, kvp.Key);
                _suspendedHotkeys[kvp.Key] = kvp.Value;
            }

            _registeredHotkeys.Clear();
        }
    }

    public void ResumeAllHotkeys()
    {
        lock (_lock)
        {
            foreach (var kvp in _suspendedHotkeys)
            {
                var info = kvp.Value;
                if (PInvoke.RegisterHotKey(_window, kvp.Key, (HOT_KEY_MODIFIERS)(uint)info.ModifierKeys,
                        info.VirtualKey))
                {
                    _registeredHotkeys[kvp.Key] = info;
                }
                else
                {
                    _logger?.LogError("恢复热键注册失败: {Hotkey}", info.Display);
                }
            }

            _suspendedHotkeys.Clear();
        }
    }

    public string GetHotkeyDisplay(int modifierKeys, uint virtualKey)
    {
        var parts = new List<string>();

        // MOD_* 位标志：Alt=0x1 Ctrl=0x2 Shift=0x4 Win=0x8，与录制器写入的值一一对应。
        if ((modifierKeys & 0x0002) != 0) parts.Add("Ctrl");
        if ((modifierKeys & 0x0001) != 0) parts.Add("Alt");
        if ((modifierKeys & 0x0004) != 0) parts.Add("Shift");
        if ((modifierKeys & 0x0008) != 0) parts.Add("Win");

        parts.Add(VirtualKeyToString(virtualKey));
        return string.Join("+", parts);
    }

    static string VirtualKeyToString(uint vk)
    {
        return vk switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),
            >= 0x60 and <= 0x69 => $"Num{vk - 0x60}",
            >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",
            _ => $"0x{vk:X2}"
        };
    }

    void EnsureWindow()
    {
        if (_window != HWND.Null)
        {
            return;
        }

        // 窗口类五件套不依赖 CsWin32 生成（这版元数据对 CreateWindowExW 的生成不稳），手写声明。
        var windowClass = new WndClassW
        {
            // 委托 thunk 指针传给 lpfnWndProc；委托由字段保活，这里只借它的地址。
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = GetModuleHandleW(null),
            lpszClassName = WindowClassName
        };
        if (RegisterClassW(ref windowClass) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "注册热键窗口类失败");
        }

        // HWND_MESSAGE(-3) 父级 = message-only 窗口：收得到消息、不可见、不进任务栏。
        _window = CreateWindowExW(0, WindowClassName, WindowClassName, 0, 0, 0, 0, 0,
            (HWND)(-3), HWND.Null, HWND.Null, null);
        if (_window == HWND.Null)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "创建热键窗口失败");
        }
    }

    nint WndProcHandler(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WmHotkey)
        {
            OnHotkey((int)wParam);
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern ushort RegisterClassW(ref WndClassW wndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern HWND CreateWindowExW(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, HWND parent, HWND menu, HWND instance, object? param);

    [DllImport("user32.dll")]
    static extern nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyWindow(HWND hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern nint GetModuleHandleW(string? moduleName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    class WndClassW
    {
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName = "";
    }

    void OnHotkey(int hotkeyId)
    {
        HotkeyInfo? info;
        lock (_lock)
        {
            _registeredHotkeys.TryGetValue(hotkeyId, out info);
        }

        if (info == null)
        {
            return;
        }

        HotkeyPressed?.Invoke(this, new SystemHotkeyEventArgs
        {
            ModifierKeys = info.ModifierKeys,
            VirtualKey = info.VirtualKey,
            HotkeyId = hotkeyId
        });
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var id in _registeredHotkeys.Keys.Concat(_suspendedHotkeys.Keys))
            {
                PInvoke.UnregisterHotKey(_window, id);
            }

            _registeredHotkeys.Clear();
            _suspendedHotkeys.Clear();
            if (_window != HWND.Null)
            {
                PInvoke.DestroyWindow(_window);
                _window = HWND.Null;
            }
        }

        GC.KeepAlive(_wndProcDelegate);
    }

    class HotkeyInfo
    {
        public int Id;
        public int ModifierKeys;
        public uint VirtualKey;
        public string Display = "";
        public int RefCount;
    }
}
