using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Platforms.Abstraction.Services;
using ClassIsland.Core.Controls;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.Core.Models.UI;
using ClassIsland.Core.Models.Weather;
using ClassIsland.Services;
using ClassIsland.Shared;
using ClassIsland.ViewModels.SettingsPages;
using ClassFabric.Assets.Localization.SettingsPage.Weather;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Views.SettingPages;

/// <summary>
/// WeatherSettingsPage.xaml 的交互逻辑
/// </summary>
[SettingsPageInfo("weather", "天气", "\uf44f", "\uf44e", SettingsPageCategory.Internal)]
public partial class WeatherSettingsPage : SettingsPageBase
{
    public WeatherSettingsViewModel ViewModel { get; } = IAppHost.GetService<WeatherSettingsViewModel>();

    private ILogger<WeatherSettingsPage> Logger => ViewModel.Logger;

    public SettingsService SettingsService { get; }

    public WeatherSettingsPage(SettingsService settingsService, IWeatherService weatherService, ILocationService locationService, ILogger<WeatherSettingsPage> logger)
    {
        InitializeComponent();
        DataContext = this;
        SettingsService = settingsService;
    }

    private void ButtonEditCurrentCity_OnClick(object sender, RoutedEventArgs e)
    {
        OpenDrawer("CitySearcher");
    }

    private void TextBoxSearchCity_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.OnSearchTextChanged(((TextBox)sender).Text);
    }

    private async void SelectorCity_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var listbox = (ListBox)sender;
        var city = (City?)listbox.SelectedItem;
        if (city == null)
        {
            e.Handled = true;
            return;
        }
        await ViewModel.SelectCityAsync(city);
    }

    private async void ButtonGetCurrentPos_OnClick(object sender, RoutedEventArgs e)
    {
        var toast = this.ShowToastRef(new ToastMessage(Localization.LocatingToast) { AutoClose = false });
        try
        {
            await ViewModel.GetCurrentPositionAsync();
            toast.Close();
            this.ShowSuccessToast(Localization.LocateSuccessToast);
        }
        catch (Exception exception)
        {
            toast.Close();
            Logger.LogError(exception, "无法获取当前位置");
            this.ShowErrorToast(string.Format(Localization.GetCurrentPositionFailedFormat, exception.Message));
        }
    }

    private void ButtonShowPos_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowLocationCoordinates();
    }

    private async void ButtonRefreshWeather_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await ViewModel.RefreshWeatherWithResultAsync();

            if (result.IsSuccess)
            {
                if (result.IsPrecisionDegraded)
                {
                    this.ShowWarningToast(string.Format(Localization.WeatherPrecisionDegradedFormat, result.DegradedPrecision));
                    this.ShowSuccessToast(Localization.WeatherRefreshSuccessToast);
                }
                else
                {
                    this.ShowSuccessToast(Localization.WeatherRefreshSuccessToast);
                }
            }
            else
            {
                this.ShowErrorToast(string.Format(Localization.WeatherRefreshFailedFormat, result.ErrorMessage));
            }
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "天气刷新失败");
            this.ShowErrorToast(string.Format(Localization.WeatherRefreshFailedFormat, exception.Message));
        }
    }

    private void OnSettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SettingsService.Settings.NoTLSWeatherRequests))
        {
            RequestRestart();
        }
    }

    private void WeatherSettingsPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Activate();
        SettingsService.Settings.PropertyChanged += OnSettingsOnPropertyChanged;
        _ = ViewModel.InitializeAsync();
    }

    private void WeatherSettingsPage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        SettingsService.Settings.PropertyChanged -= OnSettingsOnPropertyChanged;
        ViewModel.Deactivate();
    }
}
