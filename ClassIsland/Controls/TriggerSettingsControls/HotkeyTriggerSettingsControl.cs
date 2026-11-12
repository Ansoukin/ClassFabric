using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Icons;
using ClassIsland.Models.Automation.Triggers;
using ClassIsland.Platforms.Abstraction;

namespace ClassIsland.Controls.TriggerSettingsControls;

public class HotkeyTriggerSettingsControl : TriggerSettingsControlBase<HotkeyTriggerSettings>
{
    private HotkeyRecorderControl? _recorder;
    private TextBlock? _currentHotkeyText;

    public HotkeyTriggerSettingsControl()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        var tipPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 0),
            Opacity = 0.7
        };

        var tipIcon = new TextBlock
        {
            Text = FluentIcons.InfoRegular,
            FontFamily = new FontFamily("avares://ClassIsland.Core/Assets/Fonts/#FluentSystemIcons-Resizable"),
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.Parse("#666666")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

        var tipText = new TextBlock
        {
            Text = "点击上方按钮录制自定义热键组合                            \n支持 Ctrl/Alt/Shift/Win + 按键\n或除功能键外 任何单独按键",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#666666")),
            VerticalAlignment = VerticalAlignment.Center
        };

        tipPanel.Children.Add(tipIcon);
        tipPanel.Children.Add(tipText);

        // 热键录制控件容器
        var recorderContainer = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0),
            Margin = new Thickness(0, 10),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 2,
                Blur = 8,
                Color = Color.Parse("#20000000")
            })
        };

        var recorderPanel = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };

        var iconText = new TextBlock
        {
            Text = FluentIcons.KeyboardRegular,
            FontFamily = new FontFamily("avares://ClassIsland.Core/Assets/Fonts/#FluentSystemIcons-Resizable"),
            FontSize = 20,
            Foreground = new SolidColorBrush(Color.Parse("#666666")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        _recorder = new HotkeyRecorderControl
        {
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 录制状态指示器
        var statusBorder = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.Parse("#4CAF50")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 0,
                Blur = 4,
                Color = Color.Parse("#404CAF50")
            })
        };

        _recorder.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == nameof(HotkeyRecorderControl.IsRecording) && s is HotkeyRecorderControl recorder)
            {
                statusBorder.Background = recorder.IsRecording
                    ? new SolidColorBrush(Color.Parse("#FF5722"))
                    : new SolidColorBrush(Color.Parse("#4CAF50"));
            }
        };

        // 录制期间临时挂起全局热键，否则被录制的组合会先被本进程已注册的热键拦截，采不到按键
        _recorder.RecordingStarted += (s, e) => PlatformServices.SystemHotkeyService.SuspendAllHotkeys();
        _recorder.RecordingEnded += (s, e) => PlatformServices.SystemHotkeyService.ResumeAllHotkeys();
        _recorder.HotkeyRecorded += OnHotkeyRecorded;

        recorderPanel.Children.Add(iconText);
        Grid.SetColumn(iconText, 0);

        recorderPanel.Children.Add(_recorder);
        Grid.SetColumn(_recorder, 1);

        recorderPanel.Children.Add(statusBorder);
        Grid.SetColumn(statusBorder, 2);

        recorderContainer.Child = recorderPanel;

        var recorderWrapper = new StackPanel();
        recorderWrapper.Children.Add(recorderContainer);
        recorderWrapper.Children.Add(tipPanel);

        panel.Children.Add(recorderWrapper);

        _currentHotkeyText = new TextBlock
        {
            Text = "当前热键: 未设置",
            Margin = new(0, 5)
        };
        panel.Children.Add(_currentHotkeyText);

        Content = panel;
    }

    // 构造函数里 Settings 还拿不到，挂上可视树后再回读
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (_recorder == null || Settings == null) return;

        // 加载已保存的热键
        if (Settings.ModifierKeys != 0 || Settings.VirtualKey != 0)
        {
            var display = PlatformServices.SystemHotkeyService.GetHotkeyDisplay(Settings.ModifierKeys, Settings.VirtualKey);
            _recorder.SetHotkey(Settings.ModifierKeys, Settings.VirtualKey, display);
        }

        UpdateHotkeyDisplay();
    }

    private void UpdateHotkeyDisplay()
    {
        if (_currentHotkeyText != null && Settings != null)
        {
            _currentHotkeyText.Text = $"当前热键: {Settings.HotkeyDisplay}";
        }
    }

    private void OnHotkeyRecorded(object? sender, HotkeyRecordedEventArgs e)
    {
        if (Settings == null) return;

        Settings.ModifierKeys = e.ModifierKeys;
        Settings.VirtualKey = e.VirtualKey;
        Settings.HotkeyDisplay = PlatformServices.SystemHotkeyService.GetHotkeyDisplay(e.ModifierKeys, e.VirtualKey);

        if (_recorder != null)
        {
            _recorder.HotkeyDisplay = $"已设置: {Settings.HotkeyDisplay}";
        }

        UpdateHotkeyDisplay();
    }
}
