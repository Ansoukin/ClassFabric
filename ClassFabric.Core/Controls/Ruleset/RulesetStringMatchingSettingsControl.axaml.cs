using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Models.Ruleset;

namespace ClassFabric.Core.Controls.Ruleset;

/// <summary>
/// RulesetStringMatchingSettingsControl.xaml 的交互逻辑
/// </summary>
public partial class RulesetStringMatchingSettingsControl : RuleSettingsControlBase<StringMatchingSettings>
{
    /// <inheritdoc />
    public RulesetStringMatchingSettingsControl()
    {
        InitializeComponent();
    }
}
