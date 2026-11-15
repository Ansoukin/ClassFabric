using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ClassIsland.UltraCodeShared;

namespace ClassIsland.UltraCode.Views;

/// <summary>
/// 像素流动主题的配置视图。由主题页「主题配置」分类经 SettingsViewFactory 创建，
/// 滑块直接改 <see cref="UltraCodeOptions.Current"/> 全局实例，主题内绑定
/// x:Static 单例，改动即时生效，无需重启或重载主题。
/// </summary>
public partial class PixelFlowSettingsView : UserControl
{
    /// <summary>绑定用。滑块直接改这个全局实例，改完立刻生效。</summary>
    public UltraCodeOptions Options => UltraCodeOptions.Current;

    public PixelFlowSettingsView()
    {
        DataContext = this;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        UltraCodeOptions.Current.Reset();
        UltraCodePalette.Invalidate();
    }
}
