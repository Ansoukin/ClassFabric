using Avalonia.Interactivity;
using ClassFabric.Controls;
using ClassFabric.Views;

namespace ClassFabric.Models.Authorize;

public class RequestValidateAuthorizationProviderEventArgs(object? source) : RoutedEventArgs(AuthorizeProviderPresenter.RequestValidateAuthorizationProvidersEvent, source)
{
    public bool IsError { get; set; } = false;
}