using ClassFabric.ViewModels;

namespace ClassFabric.Views.WelcomePages;

public interface IWelcomePage
{
    WelcomeViewModel ViewModel { get; set; }
}