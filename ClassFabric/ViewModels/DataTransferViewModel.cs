using System;
using System.Threading.Tasks;
using ClassFabric.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace ClassFabric.ViewModels;

public partial class DataTransferViewModel(ILogger<DataTransferPage> logger) : ObservableRecipient
{
    public ILogger<DataTransferPage> Logger { get; } = logger;

    [ObservableProperty] private string _importDescription = "";
    [ObservableProperty] private int _pageIndex = 0;
    [ObservableProperty] private Func<Task>? _browseAction;
    [ObservableProperty] private Func<Task>? _performImportAction;
    [ObservableProperty] private string _importSourcePath = "";
    
    [ObservableProperty] private bool _isProfileSelected = true;
    [ObservableProperty] private bool _isSettingsSelected = true;
    [ObservableProperty] private bool _isOtherConfigSelected = true;
    
    [ObservableProperty] private bool _isExport;
}