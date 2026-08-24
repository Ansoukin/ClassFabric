using System.Windows;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Reactive;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Models.Ruleset;

namespace ClassFabric.Core.Controls.Ruleset;

/// <summary>
/// 规则集设置控件显示控件。
/// </summary>
public partial class RulesetSettingsControlPresenter : UserControl
{
    public static readonly StyledProperty<string> RuleIdProperty = AvaloniaProperty.Register<RulesetSettingsControlPresenter, string>(
        nameof(RuleId));

    public string RuleId
    {
        get => GetValue(RuleIdProperty);
        set => SetValue(RuleIdProperty, value);
    }

    public static readonly StyledProperty<Rule?> RuleProperty = AvaloniaProperty.Register<RulesetSettingsControlPresenter, Rule?>(
        nameof(Rule));

    public Rule? Rule
    {
        get => GetValue(RuleProperty);
        set => SetValue(RuleProperty, value);
    }

    /// <inheritdoc />
    public RulesetSettingsControlPresenter()
    {
        InitializeComponent();
        
        this.GetObservable(RuleIdProperty).Subscribe(new AnonymousObserver<string>(_ => UpdateContent()));
        this.GetObservable(RuleProperty).Subscribe(new AnonymousObserver<Rule?>(_ => UpdateContent()));
    }

    private void UpdateContent()
    {
        if (Rule == null || RuleId == null)
        {
            return;
        }
        if (!IRulesetService.Rules.TryGetValue(RuleId, out var info))
        {
            return;
        }

        var ruleSettings = Rule.Settings;
        RootContentPresenter.Content = RuleSettingsControlBase.GetInstance(info, ref ruleSettings);
        Rule.Settings = ruleSettings;
    }
}
