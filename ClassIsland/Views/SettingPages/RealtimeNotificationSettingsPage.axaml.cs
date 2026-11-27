using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Icons;
using ClassIsland.Services;
using ClassIsland.Shared;
using ClassIsland.ViewModels.SettingsPages;

namespace ClassIsland.Views.SettingPages;

// 页面尚未完工，先在设置目录里整项置灰（IsDisabled），完工后移除该标记即可恢复入口。
[SettingsPageInfo("realtime-notifications", "实时通知", FluentIcons.CardUiRegular, FluentIcons.CardUiRegular, SettingsPageCategory.Internal, IsDisabled = true)]
public partial class RealtimeNotificationSettingsPage : SettingsPageBase
{
    public SettingsService SettingsService { get; } = IAppHost.GetService<SettingsService>();

    public RealtimeNotificationSettingsViewModel ViewModel { get; } = IAppHost.GetService<RealtimeNotificationSettingsViewModel>();

    public RealtimeNotificationSettingsPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Page_OnLoaded(object? sender, RoutedEventArgs e) => ViewModel.Activate();

    private void Page_OnUnloaded(object? sender, RoutedEventArgs e) => ViewModel.Deactivate();
}
