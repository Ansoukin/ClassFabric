using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Extensions.UI;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.Services;
using ClassIsland.Shared;
using ClassIsland.ViewModels.SettingsPages;
using ClassFabric.Assets.Localization.SettingsPage.Refreshing;
using FluentAvalonia.UI.Controls;

namespace ClassIsland.Views.SettingPages;

[Group("classfabric.general")]
[SettingsPageInfo("refreshing", "翻新与迎新", "\ue0b8", "\ue0b9", SettingsPageCategory.Internal)]
public partial class RefreshingSettingsPage : SettingsPageBase
{
    public RefreshingSettingsViewModel ViewModel { get; } = IAppHost.GetService<RefreshingSettingsViewModel>();
    
    public RefreshingSettingsPage()
    {
        DataContext = this;
        InitializeComponent();
    }

    private void SettingsExpanderItemRefreshNow_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.RefreshingService.BeginRefresh(false);
    }

    private async void SettingsExpanderItemResetOnboardingMessages_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new FAContentDialog()
        {
            Title = Localization.ResetOnboardingMessagesDialogTitle,
            Content = Localization.ResetOnboardingMessagesDialogContent,
            PrimaryButtonText = Localization.Reset,
            DefaultButton = FAContentDialogButton.Primary,
            SecondaryButtonText = Localization.Cancel
        };
        var r = await dialog.ShowAsyncAuto(TopLevel.GetTopLevel(this));
        if (r != FAContentDialogResult.Primary)
        {
            return;
        }

        ViewModel.SettingsService.Settings.OnboardingToastTitle = RefreshingService.DefaultOnboardingToastTitle;
        ViewModel.SettingsService.Settings.OnboardingToastBody = RefreshingService.DefaultOnboardingToastBody;
        this.ShowToast(Localization.OnboardingMessagesReset);
    }

    private void SettingsExpanderItemTestOnboardingDialog_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.RefreshingService.ShowOnboardingDialog(true);
    }

    private async void ButtonReserveSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        var win = new RefreshingScopesConfigDialog()
        {
            Scopes = ViewModel.SettingsService.Settings.RefreshingScopes
        };
        await win.ShowModal(this.FindAncestorOfType<ViewBase>());
    }

    private void ButtonExit_OnClick(object? sender, RoutedEventArgs e)
    {
        AppBase.Current.Stop();
    }

    private void ButtonClearLeftToastCount_OnClick(object? sender, RoutedEventArgs e)
    {   
        ViewModel.SettingsService.Settings.LeftRefreshingToastCounts = 0;
        ViewModel.SettingsService.Settings.RefreshingToastIsOnboardingGuide = false;
    }
}
