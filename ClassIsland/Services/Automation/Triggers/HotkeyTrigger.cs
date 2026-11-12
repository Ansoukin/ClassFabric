using System;
using System.ComponentModel;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Models.Automation.Triggers;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Platforms.Abstraction.Services;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Triggers;

[AutomationCategory("事件与集成")]
[TriggerInfo("SystemTools.HotkeyTrigger", "按下自定义热键时", FluentIcons.KeyboardRegular)]
public class HotkeyTrigger : TriggerBase<HotkeyTriggerSettings>
{
    ILogger<HotkeyTrigger> Logger { get; } = App.GetService<ILogger<HotkeyTrigger>>();

    private int _hotkeyId = -1;

    public override void Loaded()
    {
        PlatformServices.SystemHotkeyService.HotkeyPressed += OnHotkeyPressed;
        Settings.PropertyChanged += OnSettingsPropertyChanged;

        RegisterHotkey();
    }

    public override void UnLoaded()
    {
        PlatformServices.SystemHotkeyService.HotkeyPressed -= OnHotkeyPressed;
        Settings.PropertyChanged -= OnSettingsPropertyChanged;

        UnregisterHotkey();

        Logger.LogDebug("热键触发器已卸载");
    }

    private void RegisterHotkey()
    {
        UnregisterHotkey();

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var modifiers = Settings.ModifierKeys;
        var vk = Settings.VirtualKey;

        // 老配置或录制中断会留下键值为 0 的设置，此时退回 F9 单键，避免拿 0 去注册
        if (vk == 0)
        {
            vk = 0x78;
            modifiers = 0;
        }

        try
        {
            _hotkeyId = PlatformServices.SystemHotkeyService.RegisterHotkey(modifiers, vk);
            Logger.LogInformation("热键触发器已加载，监听: {Hotkey}",
                PlatformServices.SystemHotkeyService.GetHotkeyDisplay(modifiers, vk));
        }
        catch (Exception ex)
        {
            // 组合被其它程序占用等注册失败只影响本触发器，不能中断整个工作流的加载
            _hotkeyId = -1;
            Logger.LogError(ex, "热键注册失败: {Modifiers}+{VirtualKey}", modifiers, vk);
        }
    }

    private void UnregisterHotkey()
    {
        if (_hotkeyId >= 0)
        {
            PlatformServices.SystemHotkeyService.UnregisterHotkey(_hotkeyId);
            _hotkeyId = -1;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 热键组合变更时重新注册
        if (e.PropertyName == nameof(HotkeyTriggerSettings.ModifierKeys) ||
            e.PropertyName == nameof(HotkeyTriggerSettings.VirtualKey))
        {
            Logger.LogInformation("热键配置已变更，重新注册热键");
            RegisterHotkey();
        }
    }

    private void OnHotkeyPressed(object? sender, SystemHotkeyEventArgs e)
    {
        // 键值为 0 回退 F9 后实际注册的组合与设置值不一致，按热键 ID 匹配事件来源
        if (e.HotkeyId != _hotkeyId)
        {
            return;
        }

        // 防抖动
        if (DateTime.Now - Settings.LastTriggered < TimeSpan.FromMilliseconds(500))
        {
            return;
        }

        Settings.LastTriggered = DateTime.Now;
        Logger.LogInformation("热键 {Hotkey} 按下，触发自动化", Settings.HotkeyDisplay);
        Trigger();
    }
}
