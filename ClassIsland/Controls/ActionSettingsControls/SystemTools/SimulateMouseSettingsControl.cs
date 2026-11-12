using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

/// <summary>
/// 「模拟鼠标」行动设置控件。低级鼠标钩子录制 + 低级键盘钩子监听 Esc 停止 + 手动编辑操作序列。
/// </summary>
public class SimulateMouseSettingsControl : ActionSettingsControlBase<MouseInputSettings>
{
    private const int WhMouseLl = 14; // WH_MOUSE_LL
    private const int WhKeyboardLl = 13; // WH_KEYBOARD_LL
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmMouseWheel = 0x020A;
    private const uint WmMouseMove = 0x0200;

    private readonly CheckBox _notifyCheckBox;
    private readonly Button _startButton;
    private readonly ListBox _actionsListBox;
    private readonly ComboBox _typeBox;
    private readonly TextBox _xBox;
    private readonly TextBox _yBox;
    private readonly TextBox _scrollBox;
    private readonly TextBox _intervalBox;
    private readonly CheckBox _dragEndBox;
    private bool _isRecording;
    private IntPtr _mouseHookId = IntPtr.Zero;
    private IntPtr _keyboardHookId = IntPtr.Zero;
    private readonly List<MouseAction> _recordedActions = [];
    private readonly Stopwatch _stopwatch = new();
    private long _lastActionTime;
    private bool _isDragging;
    private int _dragStartX;
    private int _dragStartY;
    private MouseAction? _lastDragAction;

    private LowLevelHookProc? _mouseHookProc;
    private LowLevelHookProc? _keyboardHookProc;

    /// <summary>
    /// 低级钩子回调。委托必须存进字段钉住，否则录制中委托被 GC 回收会直接崩进程。
    /// </summary>
    private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    public SimulateMouseSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "模拟鼠标操作（支持左键/右键/拖动/滚轮）",
            FontWeight = FontWeight.Bold,
            FontSize = 14
        });

        _startButton = new Button
        {
            Content = "开始录制鼠标行为",
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _startButton.Click += (s, e) => ToggleRecording();

        panel.Children.Add(_startButton);

        _actionsListBox = new ListBox
        {
            Height = 200,
            Margin = new(0, 10, 0, 0)
        };
        _actionsListBox.SelectionChanged += (s, e) => LoadSelectedAction();
        panel.Children.Add(_actionsListBox);

        var editor = new StackPanel { Spacing = 6 };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _typeBox = new ComboBox { ItemsSource = Enum.GetValues<MouseAction.ActionType>(), SelectedIndex = 0, MinWidth = 120 };
        _xBox = new TextBox { PlaceholderText = "X", Width = 70 };
        _yBox = new TextBox { PlaceholderText = "Y", Width = 70 };
        _scrollBox = new TextBox { PlaceholderText = "滚动", Width = 70 };
        _intervalBox = new TextBox { PlaceholderText = "延迟(ms)", Width = 90 };
        _dragEndBox = new CheckBox { Content = "拖动结束" };
        row.Children.Add(new TextBlock { Text = "操作", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(_typeBox);
        row.Children.Add(new TextBlock { Text = "坐标", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(_xBox);
        row.Children.Add(_yBox);
        row.Children.Add(new TextBlock { Text = "滚动", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(_scrollBox);
        row.Children.Add(new TextBlock { Text = "延迟", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(_intervalBox);
        row.Children.Add(_dragEndBox);
        editor.Children.Add(row);
        var editButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        AddEditorButton(editButtons, "应用修改", ApplySelectedAction);
        AddEditorButton(editButtons, "新增", AddActionFromEditor);
        AddEditorButton(editButtons, "删除", DeleteSelectedAction);
        editor.Children.Add(editButtons);
        panel.Children.Add(editor);

        _notifyCheckBox = new CheckBox { Content = "当执行时发出提醒" };
        _notifyCheckBox.IsCheckedChanged += (s, e) => { Settings.NotifyOnExecute = _notifyCheckBox.IsChecked ?? false; };
        panel.Children.Add(_notifyCheckBox);

        Content = panel;

        // 低级钩子是 Windows 专属，其它平台禁掉录制入口，手动编辑操作序列仍可用。
        if (!OperatingSystem.IsWindows())
        {
            _startButton.IsEnabled = false;
            ToolTip.SetTip(_startButton, "录制功能仅在 Windows 上可用");
        }
    }

    private static void AddEditorButton(StackPanel panel, string text, Action action)
    {
        var button = new Button { Content = text };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _notifyCheckBox.IsChecked = Settings.NotifyOnExecute;
        if (Settings.Actions != null)
        {
            _recordedActions.Clear();
            _recordedActions.AddRange(Settings.Actions);
            UpdateListBox();
        }
    }

    private void ToggleRecording()
    {
        if (!_isRecording)
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        if (!OperatingSystem.IsWindows()) return;
        _isRecording = true;
        _recordedActions.Clear();
        _lastActionTime = 0;
        _lastDragAction = null;
        _stopwatch.Restart();
        UpdateListBox();

        _startButton.Content = "正在录制...按下 Esc 停止";
        _startButton.IsEnabled = false;

        _mouseHookProc = MouseHookCallback;
        _keyboardHookProc = KeyboardHookCallback;

        var moduleHandle = GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName ?? "");
        _mouseHookId = SetWindowsHookEx(WhMouseLl, _mouseHookProc, moduleHandle, 0);
        _keyboardHookId = SetWindowsHookEx(WhKeyboardLl, _keyboardHookProc, moduleHandle, 0);
        if (_mouseHookId == IntPtr.Zero || _keyboardHookId == IntPtr.Zero)
        {
            // 任一钩子装不上都整体退出录制，避免半录制状态把鼠标事件吞进死循环。
            StopRecording();
        }
    }

    private void StopRecording()
    {
        _isRecording = false;
        _stopwatch.Stop();
        _lastDragAction?.IsDragEnd = true;
        _lastDragAction = null;

        _startButton.Content = "开始录制鼠标行为";
        _startButton.IsEnabled = OperatingSystem.IsWindows();

        if (_mouseHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
        }

        if (_keyboardHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero;
        }

        _mouseHookProc = null;
        _keyboardHookProc = null;

        SaveActions();
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isRecording)
        {
            var hookStruct = Marshal.PtrToStructure<Msllhookstruct>(lParam);
            var currentTime = _stopwatch.ElapsedMilliseconds;
            var interval = _lastActionTime == 0 ? 0 : currentTime - _lastActionTime;

            switch ((uint)wParam)
            {
                case WmLButtonDown:
                    _isDragging = true;
                    _dragStartX = hookStruct.Pt.X;
                    _dragStartY = hookStruct.Pt.Y;
                    _lastDragAction?.IsDragEnd = true;
                    _lastDragAction = null;
                    break;

                case WmLButtonUp:
                    // 按下与抬起之间位移超过 5 像素视为拖动，否则视为单击。
                    if (_isDragging && (Math.Abs(hookStruct.Pt.X - _dragStartX) > 5 ||
                                        Math.Abs(hookStruct.Pt.Y - _dragStartY) > 5))
                    {
                        AddAction(MouseAction.ActionType.DragMove, hookStruct.Pt.X, hookStruct.Pt.Y, 0, interval, true);
                        _lastDragAction = null;
                    }
                    else
                    {
                        AddAction(MouseAction.ActionType.LeftClick, hookStruct.Pt.X, hookStruct.Pt.Y, 0, interval);
                    }

                    _isDragging = false;
                    _lastActionTime = currentTime;
                    break;

                case WmRButtonDown:
                    AddAction(MouseAction.ActionType.RightClick, hookStruct.Pt.X, hookStruct.Pt.Y, 0, interval);
                    _lastActionTime = currentTime;
                    break;

                case WmMButtonDown:
                    AddAction(MouseAction.ActionType.MiddleClick, hookStruct.Pt.X, hookStruct.Pt.Y, 0, interval);
                    _lastActionTime = currentTime;
                    break;

                case WmMouseWheel:
                    // MOUSEPLHOOKSTRUCT 的 mouseData 高 16 位才是滚轮增量，符号要从 short 还原。
                    int delta = (short)((hookStruct.MouseData >> 16) & 0xFFFF);
                    AddAction(MouseAction.ActionType.Scroll, hookStruct.Pt.X, hookStruct.Pt.Y, delta, interval);
                    _lastActionTime = currentTime;
                    break;

                case WmMouseMove:
                    // 拖动路径按 50ms 间隔采样，密度刚好能还原轨迹又不至于条目爆炸。
                    if (_isDragging && interval > 50)
                    {
                        var action = AddAction(MouseAction.ActionType.DragMove, hookStruct.Pt.X, hookStruct.Pt.Y, 0,
                            interval);
                        _lastDragAction = action;
                        _lastActionTime = currentTime;
                    }

                    break;
            }
        }

        return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isRecording)
        {
            var hookStruct = Marshal.PtrToStructure<Kbdllhookstruct>(lParam);
            // 录制期间按 Esc（VK 0x1B）停止录制。
            if (hookStruct.VkCode == 27 && wParam == 0x100)
            {
                Dispatcher.UIThread.Post(() => StopRecording());
            }
        }

        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private MouseAction AddAction(MouseAction.ActionType type, int x, int y, int scrollDelta, long interval)
    {
        return AddAction(type, x, y, scrollDelta, interval, false);
    }

    private MouseAction AddAction(MouseAction.ActionType type, int x, int y, int scrollDelta, long interval,
        bool isDragEnd)
    {
        var action = new MouseAction
        {
            Type = type,
            X = x,
            Y = y,
            ScrollDelta = scrollDelta,
            Interval = interval,
            IsDragEnd = isDragEnd
        };

        _recordedActions.Add(action);
        _lastDragAction = isDragEnd ? null : _lastDragAction;

        // 钩子回调在装钩子的线程上执行，界面更新要贴回 UI 线程。
        Dispatcher.UIThread.Post(() => { UpdateListBox(); });

        return action;
    }

    private void LoadSelectedAction()
    {
        if (_actionsListBox.SelectedIndex < 0 || _actionsListBox.SelectedIndex >= _recordedActions.Count) return;
        var action = _recordedActions[_actionsListBox.SelectedIndex];
        _typeBox.SelectedItem = action.Type;
        _xBox.Text = action.X.ToString();
        _yBox.Text = action.Y.ToString();
        _scrollBox.Text = action.ScrollDelta.ToString();
        _intervalBox.Text = action.Interval.ToString();
        _dragEndBox.IsChecked = action.IsDragEnd;
    }

    private MouseAction? ReadEditor()
    {
        if (!int.TryParse(_xBox.Text, out var x) || !int.TryParse(_yBox.Text, out var y) ||
            !int.TryParse(_scrollBox.Text, out var scroll) || !long.TryParse(_intervalBox.Text, out var interval))
        {
            return null;
        }

        return new MouseAction
        {
            Type = (MouseAction.ActionType)(_typeBox.SelectedItem ?? MouseAction.ActionType.LeftClick),
            X = x,
            Y = y,
            ScrollDelta = scroll,
            Interval = Math.Max(0, interval),
            IsDragEnd = _dragEndBox.IsChecked ?? false
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
        _recordedActions.Add(ReadEditor() ?? new MouseAction());
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

    private void SaveActions()
    {
        Settings.Actions = [.. _recordedActions];
    }

    private void UpdateListBox()
    {
        var items = new List<string>();
        for (int i = 0; i < _recordedActions.Count; i++)
        {
            var action = _recordedActions[i];
            string actionText = $"{i + 1}. [{action.Interval}ms] ";

            switch (action.Type)
            {
                case MouseAction.ActionType.LeftClick:
                    actionText += $"左键点击 ({action.X}, {action.Y})";
                    break;
                case MouseAction.ActionType.RightClick:
                    actionText += $"右键点击 ({action.X}, {action.Y})";
                    break;
                case MouseAction.ActionType.MiddleClick:
                    actionText += $"中键点击 ({action.X}, {action.Y})";
                    break;
                case MouseAction.ActionType.Scroll:
                    actionText += $"滚动 {action.ScrollDelta} ({action.X}, {action.Y})";
                    break;
                case MouseAction.ActionType.DragMove:
                    actionText += $"拖动 ({action.X}, {action.Y})" + (action.IsDragEnd ? " [结束]" : "");
                    break;
            }

            items.Add(actionText);
        }

        _actionsListBox.ItemsSource = items;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msllhookstruct
    {
        public Point Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
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

    // 低级钩子的手写声明。这里不能用平台服务：钩子负责"听"输入，SystemInputService 只管"发"输入。
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
