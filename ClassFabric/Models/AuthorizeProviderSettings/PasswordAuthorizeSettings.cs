using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.AuthorizeProviderSettings;

public partial class PasswordAuthorizeSettings : ObservableObject
{
    [ObservableProperty]
    private string _passwordHash = "";

    [ObservableProperty]
    private byte[] _passwordSalt = [];
}