using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Attributes;

namespace ClassFabric.ExamplePlugin.Views.SettingsPages;

[SettingsPageInfo("classfabric.example-plugin.hello", "Hello world!")]
public partial class HelloSettingsPage : SettingsPageBase
{
    public HelloSettingsPage()
    {
        InitializeComponent();
    }
}