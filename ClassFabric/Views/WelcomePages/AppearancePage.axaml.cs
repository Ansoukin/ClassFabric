using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ClassFabric.ViewModels;

namespace ClassFabric.Views.WelcomePages;

public partial class AppearancePage : UserControl, IWelcomePage
{
    public AppearancePage()
    {
        InitializeComponent();
    }

    public WelcomeViewModel ViewModel { get; set; } = null!;
}