using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Services;
using Edge_tts_sharp;
using Edge_tts_sharp.Model;

namespace ClassFabric.Controls.SpeechProviderSettingsControls;

/// <summary>
/// EdgeTtsSpeechServiceSettingsControl.xaml 的交互逻辑
/// </summary>
public partial class EdgeTtsSpeechServiceSettingsControl : SpeechProviderControlBase
{
    public SettingsService SettingsService { get; }

    public List<eVoice> EdgeVoices { get; } =
        EdgeTts.GetVoice().FindAll(i => i.Locale.Contains("zh-CN"));

    public EdgeTtsSpeechServiceSettingsControl(SettingsService settingsService)
    {
        SettingsService = settingsService;
        InitializeComponent();
    }
}

