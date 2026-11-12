using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models.Automation.Triggers;

public partial class HotkeyTriggerSettings : ObservableRecipient
{
    [JsonIgnore] public DateTime LastTriggered { get; set; } = DateTime.MinValue;

    /// <summary>
    /// 热键修饰键，RegisterHotKey 的 MOD_* 位标志（0x1=Alt 0x2=Ctrl 0x4=Shift 0x8=Win）。
    /// </summary>
    [ObservableProperty] private int _modifierKeys = 0;

    /// <summary>
    /// 热键虚拟键码，默认 F9。
    /// </summary>
    [ObservableProperty] private uint _virtualKey = 0x78;

    /// <summary>
    /// 热键显示文本（如 "Ctrl+Alt+F9"）。
    /// </summary>
    [ObservableProperty] private string _hotkeyDisplay = "F9";
}
