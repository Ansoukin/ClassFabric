using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Services;
using ClassIsland.Shared;
using ClassIsland.ViewModels.SettingsPages;

namespace ClassIsland.Views;

/// <summary>
/// 开发者预览版启动提示。主按钮「确定」带下拉菜单（SplitButton）：
/// 下拉里给「下次不再显示」与「永久不再显示」——前者只压本次，后者写入设置项。
/// 选项点中后主按钮直接进入「确定」形态，一次点击关闭，不让人在弹层里再点一遍。
/// </summary>
public partial class DevPreviewNoticeWindow : ViewBase
{
    public SettingsService SettingsService { get; } = IAppHost.GetService<SettingsService>();

    public DevPreviewNoticeViewModel ViewModel { get; } = new();

    public DevPreviewNoticeWindow()
    {
        DataContext = this;
        InitializeComponent();
    }

    /// <summary>suppress=true 时把「永久不再显示」写入设置；Settings 属性改动会自动落盘。</summary>
    private void CloseWithNotice(bool suppress)
    {
        if (suppress)
        {
            SettingsService.Settings.IsDevPreviewNoticeSuppressed = true;
        }
        Close();
    }

    private void ButtonOk_OnClick(object? sender, RoutedEventArgs e)
    {
        // 已在下拉里选过「永久不再显示」的话，点确定同样落盘——选项即语义，不要求用户区分两个按钮
        CloseWithNotice(ViewModel.SuppressPermanentChosen);
    }

    private void ButtonSuppressOnce_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.SuppressOnceChosen = true;
        ViewModel.SuppressPermanentChosen = false;
    }

    private void ButtonSuppressPermanent_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.SuppressPermanentChosen = true;
    }
}
