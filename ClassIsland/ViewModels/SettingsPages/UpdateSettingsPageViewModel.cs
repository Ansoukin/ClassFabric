using ClassIsland.Services;
using ClassIsland.Services.AppUpdating;
using ClassIsland.Services.AppUpdating.Sources;
using ClassIsland.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PhainonDistributionCenter.Shared.Models.Client;
using UpdateSettingsPage = ClassIsland.Views.SettingPages.UpdateSettingsPage;

namespace ClassIsland.ViewModels.SettingsPages;

public partial class UpdateSettingsPageViewModel(ILogger<UpdateSettingsPage> logger, UpdateService updateService, SettingsService settingsService) : ObservableObject
{
    public ILogger<UpdateSettingsPage> Logger { get; } = logger;
    public UpdateService UpdateService { get; } = updateService;
    public SettingsService SettingsService { get; } = settingsService;

    [ObservableProperty] private DistributionMetadata.DistributionChannel _selectedChannel = new();

    /// <summary>
    /// 设置界面中选中的更新源索引，与 <c>Settings.UpdateSourceId</c> 双向同步。
    /// </summary>
    public int SelectedUpdateSourceIndex
    {
        get => UpdateSourceIds.GetIndex(SettingsService.Settings.UpdateSourceId);
        set
        {
            var sourceId = UpdateSourceIds.FromIndex(value);
            if (SettingsService.Settings.UpdateSourceId == sourceId)
            {
                return;
            }

            SettingsService.Settings.UpdateSourceId = sourceId;
            UpdateService.RefreshLocalChannelMetadata();
            OnPropertyChanged();
        }
    }

    [ObservableProperty] private string _changeLogDocument = "";
    [ObservableProperty] private string _newVersionChangeLogDocument = "";
}
