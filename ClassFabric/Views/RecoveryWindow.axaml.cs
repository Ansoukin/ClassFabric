using System;
using System.Windows; 
using Avalonia.Interactivity;
using ClassFabric.Core;
using ClassFabric.Core.Controls;
using ClassFabric.ViewModels;
using ClassFabric.Views.RecoveryPages;
using FluentAvalonia.UI.Navigation;

namespace ClassFabric.Views;

/// <summary>
/// RecoveryWindow.xaml 的交互逻辑
/// </summary>
public partial class RecoveryWindow : MyWindow
{
    public static RecoveryWindow? Instance { get; private set; }

    public event EventHandler? CompleteInitialized;

    public RecoveryViewModel ViewModel { get; } = new();

    public RecoveryWindow()
    {
        Instance = this;
        InitializeComponent();
        DataContext = this;
    }

    private void RecoveryWindow_OnClosed(object? sender, EventArgs e)
    {
        AppBase.Current.Stop();
    }

    private void ButtonGoBack_OnClick(object sender, RoutedEventArgs e)
    {
        MainFrame.GoBack();
    }

    private void RecoveryWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        MainFrame.Navigated += NavigationServiceOnNavigated;

        var home = new HomePage()
        {
            MainFrame = MainFrame
        };
        home.Loaded += (o, args) => CompleteInitialized?.Invoke(this, EventArgs.Empty);
        MainFrame.Content = home;
    }

    private void NavigationServiceOnNavigated(object sender, NavigationEventArgs e)
    {
        ViewModel.CanGoBack = MainFrame.CanGoBack;
    }
}