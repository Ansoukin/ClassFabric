using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ClassFabric.Controls;
using ClassFabric.Core;
using ClassFabric.Core.Helpers.UI;
using ClassFabric.Core.Models.UI;
using ClassFabric.ViewModels;
using FluentAvalonia.UI.Controls;

namespace ClassFabric.Views.WelcomePages;

public partial class WelcomePage : UserControl, IWelcomePage
{
    public WelcomeViewModel ViewModel { get; set; } = null!;
    
    public WelcomePage()
    {
        InitializeComponent();
    }

    private void ButtonNext_OnClick(object? sender, RoutedEventArgs e)
    {
        NavigateNext();
    }

    private void NavigateNext()
    {
        this.ShowToast(new ToastMessage()
        {
            Title = "欢迎使用 ClassFabric",
            Message = "欢迎使用 ClassFabric！它是基于 ClassIsland 的个人修改版本，感谢原作者 HelloWRC 与上游 ClassIsland 社区的出色工作。ClassFabric 开源免费，官方不提供任何付费支持，源码见 https://github.com/ansoukin/ClassFabric 。使用前提：您会使用 ClassIsland（https://github.com/ClassIsland/ClassIsland ）。另外，若您通过有偿协助等付费方式获得本应用，遇到问题请优先按与卖家约定的服务框架求助；若卖家未提供预期服务，请及时退款或通过其它方式维护您的权益。",
            AutoClose = false,
            Severity = InfoBarSeverity.Warning
        });
        WelcomeWindow.WelcomeNavigateForwardCommand.Execute(this);
    }

    private void Intro_OnAnimationEnd(object? sender, EventArgs e)
    {
        ContentRoot.Classes.Add("anim");
    }

    private void ButtonDataMigration_OnClick(object? sender, RoutedEventArgs e)
    {
        var welcomeWindow = TopLevel.GetTopLevel(this) as WelcomeWindow;
        if (welcomeWindow == null)
        {
            return;
        }

        welcomeWindow.Pages.Clear();
        welcomeWindow.Pages.AddRange([typeof(WelcomePage), typeof(LicensePage), typeof(DataTransferPage)]);
        
        NavigateNext();
    }

    private void ButtonEnterRecovery_OnClick(object? sender, RoutedEventArgs e)
    {
        AppBase.Current.Restart(["-m", "-r"]);
    }

    private void ButtonJoinManagement_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new JoinManagementDialog();
        dialog.ShowDialog((TopLevel.GetTopLevel(this) as Window)!);
    }
}