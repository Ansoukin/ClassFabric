using System.Collections.ObjectModel;
using Avalonia.Platform;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.ViewModels.SettingsPages;

public class WindowSettingsViewModel(SettingsService settingsService, IFancyAnimaSettings? rippleEffectSettings = null) : ObservableRecipient
{
    public SettingsService SettingsService { get; } = settingsService;

    /// <summary>
    /// 涟漪特效设置，由 FancyAnima 内置插件注册（仅 Windows）。构造参数带默认值：
    /// 非 Windows 平台未注册此服务时 DI 落默认 null，对应设置组随之隐藏。
    /// </summary>
    public IFancyAnimaSettings? RippleEffectSettings { get; } = rippleEffectSettings;

    public bool HasRippleEffectSettings => RippleEffectSettings != null;

    private ObservableCollection<Screen> _screens = new();

    public ObservableCollection<Screen> Screens
    {
        get => _screens;
        set
        {
            if (Equals(value, _screens)) return;
            _screens = value;
            OnPropertyChanged();
        }
    }
}
