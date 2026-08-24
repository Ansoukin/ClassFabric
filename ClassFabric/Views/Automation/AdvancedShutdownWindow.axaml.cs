using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassFabric.Services.Automation.PlatformTools;

namespace ClassFabric.Views.Automation;

public partial class AdvancedShutdownWindow : Window
{
    readonly PowerService _powerService;
    readonly DispatcherTimer _countdownTimer;
    int _remainingSeconds;

    public AdvancedShutdownWindow(int totalSeconds, PowerService powerService)
    {
        InitializeComponent();
        _powerService = powerService;
        _remainingSeconds = Math.Max(totalSeconds, 0);
        UpdateCountdownText();
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _countdownTimer.Tick += CountdownTimer_Tick;
    }

    void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        if (_remainingSeconds > 0) _remainingSeconds--;
        UpdateCountdownText();
        if (_remainingSeconds == 0) _countdownTimer.Stop();
    }

    void UpdateCountdownText()
    {
        CountdownText.Text = $"{_remainingSeconds / 60:00}:{_remainingSeconds % 60:00}";
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _countdownTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer.Stop();
        base.OnClosed(e);
    }

    void ExtendButton_Click(object? sender, RoutedEventArgs e)
    {
        // 先撤销系统计划，再按“当前剩余+5分钟”重新排程；窗口倒计时同步重置到新时长
        var newTotalSeconds = _remainingSeconds + 300;
        _powerService.CancelShutdown();
        _powerService.ScheduleShutdown(newTotalSeconds);
        _remainingSeconds = newTotalSeconds;
        UpdateCountdownText();
        if (!_countdownTimer.IsEnabled)
            _countdownTimer.Start();
    }

    void CancelShutdownButton_Click(object? sender, RoutedEventArgs e)
    {
        _powerService.CancelShutdown();
        Close();
    }

    void AcknowledgeButton_Click(object? sender, RoutedEventArgs e)
    {
        // 仅关闭窗口，系统的关机计划保持继续执行
        Close();
    }
}
