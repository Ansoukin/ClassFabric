using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

/// <summary>
/// 「模拟键盘」行动设置控件。低级键盘钩子录制 + 手动编辑按键序列。
/// </summary>
public class SimulateKeyboardSettingsControl : ActionSettingsControlBase<KeyboardInputSettings>
{
    private const int WhKeyboardLl = 13; // WH_KEYBOARD_LL

    private readonly CheckBox _notifyCheckBox;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly ListBox _actionsListBox;
    private readonly ComboBox _typeBox;
    private readonly TextBox _keyCodeBox;
    private readonly TextBox _keyNameBox;
    private readonly TextBox _intervalBox;
    private bool _isRecording;
    private IntPtr _hookId = IntPtr.Zero;
    private readonly List<KeyboardAction> _recordedActions = [];
    private readonly Stopwatch _stopwatch = new();
    private long _lastActionTime;
    private LowLevelKeyboardProc? _hookProc;

    /// <summary>
    /// 低级键盘钩子回调。委托必须存进字段钉住，否则录制中委托被 GC 回收会直接崩进程。
    /// </summary>
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    public SimulateKeyboardSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };
        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        _startButton = new Button { Content = "开始录制键盘输入", Width = 150 };
        _startButton.Click += (_, _) => StartRecording();
        _stopButton = new Button { Content = "结束录制", Width = 100, IsVisible = false };
        _stopButton.Click += (_, _) => StopRecording();
        buttonPanel.Children.Add(_startButton);
        buttonPanel.Children.Add(_stopButton);
        panel.Children.Add(buttonPanel);

        _actionsListBox = new ListBox { Height = 160 };
        _actionsListBox.SelectionChanged += (_, _) => LoadSelectedAction();
        panel.Children.Add(_actionsListBox);

        var editor = new StackPanel { Spacing = 6 };
        _typeBox = new ComboBox { ItemsSource = Enum.GetValues<KeyboardAction.ActionType>(), SelectedIndex = 0, MinWidth = 120 };
        _keyCodeBox = new TextBox { PlaceholderText = "虚拟键码", Width = 90 };
        _keyNameBox = new TextBox { PlaceholderText = "按键名称", Width = 120 };
        _intervalBox = new TextBox { PlaceholderText = "延迟(ms)", Width = 90 };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = "操作", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(_typeBox);
        row.Children.Add(new TextBlock { Text = "键码", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(_keyCodeBox);
        row.Children.Add(new TextBlock { Text = "名称", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(_keyNameBox);
        row.Children.Add(new TextBlock { Text = "延迟", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(_intervalBox);
        editor.Children.Add(row);

        var editButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        AddButton(editButtons, "应用修改", ApplySelectedAction);
        AddButton(editButtons, "新增", AddActionFromEditor);
        AddButton(editButtons, "删除", DeleteSelectedAction);
        editor.Children.Add(editButtons);
        panel.Children.Add(editor);
        _notifyCheckBox = new CheckBox { Content = "当执行时发出提醒" };
        _notifyCheckBox.IsCheckedChanged += (s, e) => { Settings.NotifyOnExecute = _notifyCheckBox.IsChecked ?? false; };
        panel.Children.Add(_notifyCheckBox);

        Content = panel;

        // 低级钩子是 Windows 专属，其它平台禁掉录制入口，手动编辑按键序列仍可用。
        if (!OperatingSystem.IsWindows())
        {
            _startButton.IsEnabled = false;
            ToolTip.SetTip(_startButton, "录制功能仅在 Windows 上可用");
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _notifyCheckBox.IsChecked = Settings.NotifyOnExecute;
        _recordedActions.Clear();
        if (Settings.Actions.Count > 0)
        {
            _recordedActions.AddRange(Settings.Actions);
        }
        else
        {
            // 兼容旧版 "键码:按键名" 录制数据，转成新版操作序列。
            foreach (var key in Settings.Keys)
            {
                var parts = key.Split(':', 2);
                if (byte.TryParse(parts[0], out var keyCode))
                {
                    _recordedActions.Add(new KeyboardAction
                    {
                        KeyCode = keyCode,
                        KeyName = parts.Length > 1 ? parts[1] : keyCode.ToString(),
                        Interval = _recordedActions.Count == 0 ? 0 : 100
                    });
                }
            }
        }
        SaveActions();
        UpdateListBox();
    }

    private static void AddButton(StackPanel panel, string text, Action action)
    {
        var button = new Button { Content = text };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }

    private void StartRecording()
    {
        if (!OperatingSystem.IsWindows()) return;
        _isRecording = true;
        _recordedActions.Clear();
        _lastActionTime = 0;
        _stopwatch.Restart();
        UpdateListBox();
        _startButton.IsVisible = false;
        _stopButton.IsVisible = true;
        _hookProc = HookCallback;
        _hookId = SetWindowsHookEx(WhKeyboardLl, _hookProc,
            GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName), 0);
        if (_hookId == IntPtr.Zero)
        {
            // 钩子装不上（极少见，多为权限问题），回退到非录制状态，避免停留在"结束录制"按钮上。
            _isRecording = false;
            _hookProc = null;
            _startButton.IsVisible = true;
            _stopButton.IsVisible = false;
        }
    }

    private void StopRecording()
    {
        _isRecording = false;
        _stopwatch.Stop();
        _startButton.IsVisible = true;
        _stopButton.IsVisible = false;
        UnhookWindowsHookExSafe();
        _hookProc = null;
        SaveActions();
    }

    private void UnhookWindowsHookExSafe()
    {
        if (_hookId == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // WM_KEYDOWN = 0x100；只记按下，不记释放，一次按键对应一条 Press 操作。
        if (nCode >= 0 && _isRecording && wParam == 0x100)
        {
            var hookStruct = Marshal.PtrToStructure<Kbdllhookstruct>(lParam);
            var currentTime = _stopwatch.ElapsedMilliseconds;
            var interval = _lastActionTime == 0 ? 0 : currentTime - _lastActionTime;
            _lastActionTime = currentTime;
            var keyName = VirtualKeyNameHelper.GetKeyName(hookStruct.VkCode);
            _recordedActions.Add(new KeyboardAction
            {
                Type = KeyboardAction.ActionType.Press,
                KeyCode = (byte)hookStruct.VkCode,
                KeyName = keyName,
                Interval = interval
            });
            // 钩子回调在装钩子的线程上执行，界面更新要贴回 UI 线程。
            Dispatcher.UIThread.Post(() => { SaveActions(); UpdateListBox(); });
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void LoadSelectedAction()
    {
        if (_actionsListBox.SelectedIndex < 0 || _actionsListBox.SelectedIndex >= _recordedActions.Count) return;
        var action = _recordedActions[_actionsListBox.SelectedIndex];
        _typeBox.SelectedItem = action.Type;
        _keyCodeBox.Text = action.KeyCode.ToString();
        _keyNameBox.Text = action.KeyName;
        _intervalBox.Text = action.Interval.ToString();
    }

    private KeyboardAction? ReadEditor()
    {
        if (!byte.TryParse(_keyCodeBox.Text, out var keyCode) || !long.TryParse(_intervalBox.Text, out var interval))
            return null;
        return new KeyboardAction
        {
            Type = (KeyboardAction.ActionType)(_typeBox.SelectedItem ?? KeyboardAction.ActionType.Press),
            KeyCode = keyCode,
            KeyName = string.IsNullOrWhiteSpace(_keyNameBox.Text) ? keyCode.ToString() : _keyNameBox.Text!,
            Interval = Math.Max(0, interval)
        };
    }

    private void ApplySelectedAction()
    {
        var action = ReadEditor();
        if (action == null || _actionsListBox.SelectedIndex < 0 ||
            _actionsListBox.SelectedIndex >= _recordedActions.Count) return;
        _recordedActions[_actionsListBox.SelectedIndex] = action;
        SaveActions();
        UpdateListBox();
    }

    private void AddActionFromEditor()
    {
        var action = ReadEditor() ??
                     new KeyboardAction { Type = KeyboardAction.ActionType.Press, KeyCode = 13, KeyName = "Enter", Interval = 0 };
        _recordedActions.Add(action);
        SaveActions();
        UpdateListBox();
    }

    private void DeleteSelectedAction()
    {
        if (_actionsListBox.SelectedIndex < 0 || _actionsListBox.SelectedIndex >= _recordedActions.Count) return;
        _recordedActions.RemoveAt(_actionsListBox.SelectedIndex);
        SaveActions();
        UpdateListBox();
    }

    /// <summary>
    /// 写回设置。Keys 与 Actions 同步双写：老版本键名格式靠 Keys 保留兼容。
    /// </summary>
    private void SaveActions()
    {
        Settings.Actions = [.. _recordedActions];
        Settings.Keys = _recordedActions.ConvertAll(x => $"{x.KeyCode}:{x.KeyName}");
    }

    private void UpdateListBox()
    {
        var items = new List<string>();
        for (var i = 0; i < _recordedActions.Count; i++)
        {
            var a = _recordedActions[i];
            items.Add($"{i + 1}. [{a.Interval}ms] {a.Type} {a.KeyName} ({a.KeyCode})");
        }
        _actionsListBox.ItemsSource = items;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Kbdllhookstruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    // 低级键盘钩子的手写声明。这里不能用平台服务：钩子负责"听"按键，SystemInputService 只管"发"按键。
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
