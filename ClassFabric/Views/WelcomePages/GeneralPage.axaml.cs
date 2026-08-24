using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ClassFabric.ViewModels;

namespace ClassFabric.Views.WelcomePages;

public partial class GeneralPage : UserControl, IWelcomePage
{
    public GeneralPage()
    {
        InitializeComponent();
    }

    public WelcomeViewModel ViewModel { get; set; } = null!;

    private void ButtonNext_OnClick(object? sender, RoutedEventArgs e)
    {
        WelcomeWindow.WelcomeNavigateForwardCommand.Execute(this);
    }
}