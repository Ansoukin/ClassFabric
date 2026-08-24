#if false
using System.Windows;
using ClassFabric.Core;
using ClassFabric.Core.Abstractions.Services.Management;
using ClassFabric.Shared;
using ClassFabric.Shared.Models;
using CommunityToolkit.Mvvm.Input;

namespace ClassFabric.Views;

/// <summary>
/// ConfigErrorsWindow.xaml 的交互逻辑
/// </summary>
public partial class ConfigErrorsWindow
{
    public ConfigErrorsWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    [RelayCommand]
    private void CopyErrorDetails(ConfigError error)
    {
        Clipboard.SetDataObject(error.Exception.ToString(), false);
    }

    private async void ButtonRestoreBackups_OnClick(object sender, RoutedEventArgs e)
    {
        var managementService = IAppHost.GetService<IManagementService>();
        if (!await managementService.AuthorizeByLevel(managementService.CredentialConfig.ExitApplicationAuthorizeLevel))
        {
            return;
        }
        AppBase.Current.Restart(["-m", "-r"]);
    }
}
#endif
