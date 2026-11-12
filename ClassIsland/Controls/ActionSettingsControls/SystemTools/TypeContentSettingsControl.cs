using Avalonia.Controls;
using Avalonia.Data;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

/// <summary>
/// 「键入内容」行动设置控件。
/// </summary>
public class TypeContentSettingsControl : ActionSettingsControlBase<TypeContentSettings>
{
    private CheckBox _notifyCheckBox;
    private TextBox _textBox;

    public TypeContentSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "要键入的内容:",
            FontWeight = Avalonia.Media.FontWeight.Bold
        });

        _textBox = new TextBox
        {
            PlaceholderText = "输入要粘贴的文本内容",
            AcceptsReturn = true,
            Height = 100
        };

        panel.Children.Add(_textBox);
        _notifyCheckBox = new CheckBox { Content = "当执行时发出提醒" };
        _notifyCheckBox.IsCheckedChanged += (s, e) => { Settings.NotifyOnExecute = _notifyCheckBox.IsChecked ?? false; };
        panel.Children.Add(_notifyCheckBox);

        Content = panel;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _notifyCheckBox.IsChecked = Settings.NotifyOnExecute;
        // 设置模型是普通 POCO，没有变更通知，双向绑定靠 TextBox 的默认 TwoWay 模式把编辑写回。
        _textBox.Bind(TextBox.TextProperty, new Binding(nameof(Settings.Content))
        {
            Source = Settings
        });
    }
}
