using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Services;
using ClassFabric.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.ViewModels.SettingsPages;

public partial class AdvancedSettingsViewModel(SettingsService settingsService) : ObservableObject
{
    public SettingsService SettingsService { get; } = settingsService;
    
    public int RenderingModeSelectedIndex
    {
        get => int.TryParse(GlobalStorageService.GetValue("Win32RenderingMode") ?? "", out var v1)
            ? v1
            : 0;
        set
        {
            GlobalStorageService.SetValue("Win32RenderingMode", value.ToString());
            OnPropertyChanged();
        }
    }
    
    public bool UseNativeTitlebar
    {
        get => bool.TryParse(GlobalStorageService.GetValue("UseNativeTitlebar"), out var v)
            ? v
            : IThemeService.UseNativeTitlebar;
        set
        {
            GlobalStorageService.SetValue("UseNativeTitlebar", value.ToString());
            OnPropertyChanged();
        } 
    }
    
    public bool IgnoreQtScaling
    {
        get => GlobalStorageService.GetValue("IgnoreQtScaling") == "1";
        set
        {
            GlobalStorageService.SetValue("IgnoreQtScaling", value ? "1" : "0");
            OnPropertyChanged();
        } 
    }
}