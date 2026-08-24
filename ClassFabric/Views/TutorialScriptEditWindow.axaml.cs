using System;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using ClassFabric.Core.Controls;
using ClassFabric.Core.Models.Tutorial;

namespace ClassFabric.Views;

public partial class TutorialScriptEditWindow : MyWindow
{
    public static readonly StyledProperty<TutorialSentence?> TutorialSentenceProperty =
        AvaloniaProperty.Register<TutorialScriptEditWindow, TutorialSentence?>(nameof(TutorialSentence));

    private bool _isUpdatingEditor;

    public TutorialSentence? TutorialSentence
    {
        get => GetValue(TutorialSentenceProperty);
        set => SetValue(TutorialSentenceProperty, value);
    }

    public TutorialScriptEditWindow()
    {
        InitializeComponent();
        DataContext = this;

        ScriptEditor.SyntaxHighlighting = GetLuaHighlightingDefinition();
        ScriptEditor.Text = TutorialSentence?.Script ?? "";
        this.GetObservable(TutorialSentenceProperty).Subscribe(OnTutorialSentenceChanged);
    }

    private void OnTutorialSentenceChanged(TutorialSentence? sentence)
    {
        _isUpdatingEditor = true;
        ScriptEditor.Text = sentence?.Script ?? "";
        _isUpdatingEditor = false;
    }

    private void ScriptEditor_OnTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingEditor || TutorialSentence == null)
        {
            return;
        }

        TutorialSentence.Script = ScriptEditor.Text;
    }

    private static IHighlightingDefinition? GetLuaHighlightingDefinition()
    {
        const string name = "Lua";
        if (HighlightingManager.Instance.GetDefinition(name) is { } definition)
        {
            return definition;
        }

        using var stream = AssetLoader.Open(new Uri("avares://ClassFabric/Assets/Highlighting/Lua.xshd"));
        using var reader = XmlReader.Create(stream);
        definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting(name, [".lua"], definition);
        return definition;
    }
}
