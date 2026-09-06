using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;

namespace ClassIsland.ExamplePlugin.Views.SettingsPages;

[SettingsPageInfo("classfabric.example-plugin.hello", "Hello world!")]
public partial class HelloSettingsPage : SettingsPageBase
{
    public HelloSettingsPage()
    {
        InitializeComponent();
    }
}
