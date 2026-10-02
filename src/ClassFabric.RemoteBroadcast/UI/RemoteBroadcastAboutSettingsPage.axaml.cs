using ClassFabric.RemoteBroadcast.Models;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Icons;

namespace ClassFabric.RemoteBroadcast.UI;

/// <summary>
/// 远程广播「关于」页：插件身份 + 四步使用流程。
/// 版本号走注入的 <see cref="RemoteBroadcastDescriptor"/>，页面不直接摸插件清单。
/// </summary>
[SettingsPageInfo("remoteBroadcast.about", "关于", FluentIcons.InfoRegular, FluentIcons.InfoRegular,
    SettingsPageCategory.About)]
[Group("remoteBroadcast")]
public partial class RemoteBroadcastAboutSettingsPage : SettingsPageBase
{
    public RemoteBroadcastAboutSettingsPage(RemoteBroadcastDescriptor descriptor)
    {
        InitializeComponent();
        VersionText.Text = $"版本 {descriptor.Version}";
    }
}
