using System.Collections.ObjectModel;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Authorize;
using CommunityToolkit.Mvvm.ComponentModel;
using AuthorizeProviderDisplayingModel = ClassFabric.Models.Authorize.AuthorizeProviderDisplayingModel;

namespace ClassFabric.ViewModels;

public partial class AuthorizeViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<AuthorizeProviderDisplayingModel> _providers = [];

    [ObservableProperty] private bool _isEditingMode = false;

    [ObservableProperty] private Credential _credential = new();

    [ObservableProperty] private AuthorizeProviderInfo? _selectedAuthorizeProviderInfo;

    [ObservableProperty] private CredentialItem? _selectedCredentialItem;
}