using System;
using System.ComponentModel;
using Avalonia.Threading;
using ClassFabric.Views;
using Microsoft.Extensions.Logging;

namespace ClassFabric.Services;

/// <summary>
/// 竖版课表窗口生命周期服务：订阅总开关整体显隐窗口，持 50ms 定时器驱动重定位与状态重断言。
/// </summary>
public class VerticalScheduleService
{
    private ILogger<VerticalScheduleService> Logger { get; }
    private SettingsService SettingsService { get; }
    private VerticalScheduleWindow Window { get; }

    private DispatcherTimer Timer { get; } = new()
    {
        Interval = TimeSpan.FromMilliseconds(50)
    };

    private bool _isInitialized = false;

    public VerticalScheduleService(ILogger<VerticalScheduleService> logger, SettingsService settingsService,
        VerticalScheduleWindow window)
    {
        Logger = logger;
        SettingsService = settingsService;
        Window = window;
        Timer.Tick += TimerOnTick;
    }

    /// <summary>
    /// 初始化服务。必须在 MainWindow.Show 之后调用，避免竖窗先于主窗出现。
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        SettingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
        if (SettingsService.Settings.VerticalScheduleSettings.IsEnabled)
        {
            ShowWindow();
        }
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 嵌套设置的子属性变更统一转发为容器属性通知（见 Settings.VerticalScheduleSettingsOnPropertyChanged）
        if (e.PropertyName != nameof(SettingsService.Settings.VerticalScheduleSettings)) return;
        // 仅总开关 IsEnabled 控制窗口整体显隐；规则性隐藏由窗口内容根折叠承担
        if (SettingsService.Settings.VerticalScheduleSettings.IsEnabled)
        {
            ShowWindow();
        }
        else
        {
            HideWindow();
        }
    }

    private void ShowWindow()
    {
        if (Window.IsVisible) return;
        Logger.LogInformation("显示竖版课表窗口。");
        Window.Show();
        // 定时器仅窗口可见时运行
        Timer.Start();
    }

    private void HideWindow()
    {
        Timer.Stop();
        if (!Window.IsVisible) return;
        Logger.LogInformation("隐藏竖版课表窗口。");
        Window.Hide();
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        // 每 tick 重算定位与可见性，并重断言层级与穿透；该轮询为横条重查模式 0/1/2 的功能超集
        Window.UpdateWindowPos();
        Window.UpdateVisibility();
        Window.UpdateWindowLayer();
        Window.UpdateWindowFeatures();
        // 全天进度细轨随同一节拍按真实时间推进
        Window.UpdateDayRail();
        // 上下课提醒状态机随同一节拍推进（全关时窗口内自行旁路）
        Window.UpdateReminder();
    }
}
