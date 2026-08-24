using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.ViewModels.SettingsPages;

public partial class RefreshingSettingsViewModel(
    SettingsService settingsService, 
    ITutorialService tutorialService,
    IRefreshingService refreshingService) : ObservableObject
{
    public SettingsService SettingsService { get; } = settingsService;
    public ITutorialService TutorialService { get; } = tutorialService;
    public IRefreshingService RefreshingService { get; } = refreshingService;
}