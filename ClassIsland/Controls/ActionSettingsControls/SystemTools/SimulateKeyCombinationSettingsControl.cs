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
/// 「模拟组合键」行动设置控件。每个键位点「录入」后按下一个键即完成捕捉。
/// </summary>
public class SimulateKeyCombinationSettingsControl : ActionSettingsControlBase<KeyCombinationSettings>
{
    private const int MinKeyRows = 2;
    private const int MaxKeyRows = 5;
    private const int WhKeyboardLl = 13; // WH_KEYBOARD_LL
    private const uint WmKeyDown = 0x0100;
    private const uint WmSysKeyDown = 0x0104;

    private readonly StackPanel _keysPanel;
    private readonly Button _addButton;
    private readonly CheckBox _notifyCheckBox;
    private readonly List<KeyCombinationKey> _keySlots = [];
    private bool _isRecording;
    private int? _recordingIndex;
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;

    /// <summary>
    /// 低级键盘钩子回调。委托必须存进字段钉住，否则录制中委托被 GC 回收会直接崩进程。
    /// </summary>
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    public SimulateKeyCombinationSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };
        panel.Children.Add(new TextBlock
        {
            Text = "设置要同时按下的按键。点击“录入”后按下一个键即可保存。"
        });

        _keysPanel = new StackPanel { Spacing = 8 };
        panel.Children.Add(_keysPanel);

        _addButton = new Button { Content = "添加更多键" };
        _addButton.Click += (_, _) => AddKeySlot();
        panel.Children.Add(_addButton);

        _notifyCheckBox = new CheckBox { Content = "当执行时发出提醒" };
        _notifyCheckBox.IsCheckedChanged += (_, _) => { Settings.NotifyOnExecute = _notifyCheckBox.IsChecked ?? false; };
        panel.Children.Add(_notifyCheckBox);

        Content = panel;

        // 低级钩子是 Windows 专属，其它平台禁掉录入入口，键码仍可手动配置。
        if (!OperatingSystem.IsWindows())
        {
            _addButton.IsEnabled = false;
            ToolTip.SetTip(_addButton, "录入功能仅在 Windows 上可用");
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _notifyCheckBox.IsChecked = Settings.NotifyOnExecute;
        _keySlots.Clear();
        _keySlots.AddRange(Settings.Keys);
        NormalizeKeySlots();
        SaveKeys();
        RenderRows();
    }

    private void AddKeySlot()
    {
        if (_keySlots.Count >= MaxKeyRows)
        {
            return;
        }

        _keySlots.Add(new KeyCombinationKey());
        SaveKeys();
        RenderRows();
    }

    private void DeleteKeySlot(int index)
    {
        if (index < MinKeyRows || index >= _keySlots.Count)
        {
            return;
        }

        if (_recordingIndex == index)
        {
            StopRecording();
        }

        _keySlots.RemoveAt(index);
        NormalizeKeySlots();
        SaveKeys();
        RenderRows();
    }

    private void StartRecording(int index)
    {
        if (!OperatingSystem.IsWindows() || index < 0 || index >= _keySlots.Count)
        {
            return;
        }

        StopRecording(false);
        _isRecording = true;
        _recordingIndex = index;
        _hookProc = HookCallback;
        _hookId = SetWindowsHookEx(WhKeyboardLl, _hookProc,
            GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName), 0);
        if (_hookId == IntPtr.Zero)
        {
            _isRecording = false;
            _recordingIndex = null;
            _hookProc = null;
        }

        RenderRows();
    }

    private void StopRecording(bool renderRows = true)
    {
        _isRecording = false;
        _recordingIndex = null;
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        _hookProc = null;
        if (renderRows)
        {
            RenderRows();
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // 组合键修饰键（Alt 等）按下来的是 WM_SYSKEYDOWN，也要接住。
        if (nCode >= 0 && _isRecording && (wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown) &&
            _recordingIndex is { } index)
        {
            var hookStruct = Marshal.PtrToStructure<Kbdllhookstruct>(lParam);
            if (hookStruct.VkCode <= byte.MaxValue)
            {
                _isRecording = false;
                var keyCode = (byte)hookStruct.VkCode;
                var keyName = VirtualKeyNameHelper.GetKeyName(hookStruct.VkCode);
                // 钩子回调在装钩子的线程上执行，界面更新要贴回 UI 线程。
                Dispatcher.UIThread.Post(() => CompleteRecording(index, keyCode, keyName));
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void CompleteRecording(int index, byte keyCode, string keyName)
    {
        if (index >= 0 && index < _keySlots.Count)
        {
            _keySlots[index] = new KeyCombinationKey
            {
                KeyCode = keyCode,
                KeyName = keyName
            };
            SaveKeys();
        }

        StopRecording();
    }

    private void NormalizeKeySlots()
    {
        while (_keySlots.Count < MinKeyRows)
        {
            _keySlots.Add(new KeyCombinationKey());
        }

        if (_keySlots.Count > MaxKeyRows)
        {
            _keySlots.RemoveRange(MaxKeyRows, _keySlots.Count - MaxKeyRows);
        }
    }

    private void SaveKeys()
    {
        Settings.Keys = [.. _keySlots];
    }

    private void RenderRows()
    {
        _keysPanel.Children.Clear();
        for (var i = 0; i < _keySlots.Count; i++)
        {
            var index = i;
            var key = _keySlots[index];
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };

            row.Children.Add(new TextBlock
            {
                Text = $"按键 {index + 1}",
                Width = 60,
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBox
            {
                Text = string.IsNullOrWhiteSpace(key.KeyName) ? string.Empty : $"{key.KeyName} ({key.KeyCode})",
                PlaceholderText = "未录入",
                Width = 180,
                IsReadOnly = true
            });

            var recordButton = new Button
            {
                Content = _recordingIndex == index ? "录入中…" : "录入",
                // Windows 以外的平台一律禁用录入：装钩子没有意义。
                IsEnabled = OperatingSystem.IsWindows() && (!_isRecording || _recordingIndex == index)
            };
            recordButton.Click += (_, _) => StartRecording(index);
            row.Children.Add(recordButton);

            if (index >= MinKeyRows)
            {
                var deleteButton = new Button { Content = "删除", IsEnabled = !_isRecording };
                deleteButton.Click += (_, _) => DeleteKeySlot(index);
                row.Children.Add(deleteButton);
            }

            _keysPanel.Children.Add(row);
        }

        // 非 Windows 没有录入钩子，添加更多键在这里一并禁用，避免只在构造时禁用又被这里解开。
        _addButton.IsEnabled = OperatingSystem.IsWindows() && _keySlots.Count < MaxKeyRows && !_isRecording;
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
