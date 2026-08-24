using System;
using System.Collections.ObjectModel;
using ClassFabric.Core.Abstractions.Services.Management;
using ClassFabric.Models;
using ClassFabric.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.ViewModels.SettingsPages;

public class AboutSettingsViewModel(IManagementService managementService, SettingsService settingsService, DiagnosticService diagnosticService) : ObservableRecipient
{
    public IManagementService ManagementService { get; } = managementService;
    public SettingsService SettingsService { get; } = settingsService;
    public DiagnosticService DiagnosticService { get; } = diagnosticService;
    private string _diagnosticInfo = "";
    private bool _isRefreshingContributors;
    private string _license = "";
    private ObservableCollection<NuGetLicenseInfo> _thirdPartyLibs = [];
    private int _clickCount = 0;
    private int _appInfoClickCount = 0;

    public string DiagnosticInfo
    {
        get => _diagnosticInfo;
        set
        {
            if (value == _diagnosticInfo) return;
            _diagnosticInfo = value;
            OnPropertyChanged();
        }
    }

    public bool IsRefreshingContributors
    {
        get => _isRefreshingContributors;
        set
        {
            if (value == _isRefreshingContributors) return;
            _isRefreshingContributors = value;
            OnPropertyChanged();
        }
    }

    public string License
    {
        get => _license;
        set
        {
            if (value == _license) return;
            _license = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<NuGetLicenseInfo> ThirdPartyLibs
    {
        get => _thirdPartyLibs;
        set
        {
            if (Equals(value, _thirdPartyLibs)) return;
            _thirdPartyLibs = value;
            OnPropertyChanged();
        }
    }

    public int ClickCount
    {
        get => _clickCount;
        set
        {
            if (value == _clickCount) return;
            _clickCount = value;
            OnPropertyChanged();
        }
    }

    public int AppInfoClickCount
    {
        get => _appInfoClickCount;
        set
        {
            if (value == _appInfoClickCount) return;
            _appInfoClickCount = value;
            OnPropertyChanged();
        }
    }
}
