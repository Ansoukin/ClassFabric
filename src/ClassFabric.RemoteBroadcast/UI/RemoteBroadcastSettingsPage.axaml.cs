using ClassIsland.Core.Abstractions.Controls;

namespace ClassFabric.RemoteBroadcast.UI;

/// <summary>
/// 远程广播设置页：M0 为占位页，M1 接入名单管理、接入码/二维码与播报设置。
/// </summary>
[ClassIsland.Core.Attributes.SettingsPageInfo("remoteBroadcast", "远程广播")]
public partial class RemoteBroadcastSettingsPage : SettingsPageBase
{
    public RemoteBroadcastSettingsPage()
    {
        InitializeComponent();
    }
}
