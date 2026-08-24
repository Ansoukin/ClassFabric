using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.Rules;

namespace ClassFabric.Controls.RuleSettingsControls;

public partial class SunRiseSetRuleSettingsControl : RuleSettingsControlBase<SunRiseSetRuleSettings>
{
    public SunRiseSetRuleSettingsControl()
    {
        InitializeComponent();
    }
}
