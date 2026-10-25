using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Models.Automation.Triggers;
using Microsoft.Extensions.Logging;
using TimeCrontab;

namespace ClassIsland.Services.Automation.Triggers;

[TriggerInfo("classisland.cron", "cron", "\ue125")]
public class CronTrigger : TriggerBase<CronTriggerSettings>
{
    // Task.Delay 的正常调度误差在毫秒级；超过该容差视为应用内时间发生过跳变
    // （时间偏移调整、NTP 校时、系统时间修改、休眠唤醒），错过的时间点不做补触发。
    private const double OccurrenceToleranceSeconds = 5;

    private Crontab? _crontab;

    private CancellationTokenSource? _stopCancellationTokenSource;

    ILogger<CronTrigger> Logger { get; } = App.GetService<ILogger<CronTrigger>>();
    IExactTimeService ExactTimeService { get; } = App.GetService<IExactTimeService>();

    public override void Loaded()
    {
        Settings.PropertyChanged += SettingsOnPropertyChanged;
        _stopCancellationTokenSource = new CancellationTokenSource();
        LoadCron();
    }

    private async Task CronWorker()
    {
        while (_stopCancellationTokenSource?.IsCancellationRequested == false)
        {
            if (_crontab == null)
            {
                await Task.Delay(1000, _stopCancellationTokenSource.Token);
                continue;
            }

            var now = ExactTimeService.GetCurrentLocalDateTime();
            var expectedOccurrence = now + _crontab.GetSleepTimeSpan(now);
            await Task.Delay(expectedOccurrence - now, _stopCancellationTokenSource.Token);
            if (_stopCancellationTokenSource.IsCancellationRequested)
            {
                break;
            }

            // 时间偏移调整、NTP 校时、系统时间修改都会让应用内时间相对墙钟跳变，
            // 按睡眠时长醒来后实际时刻可能已经偏离预期发生点（时间偏移调整时表现为
            // 新旧两条时间线各触发一次）。偏离预期发生点时不触发，按当前时间重新对齐。
            var after = ExactTimeService.GetCurrentLocalDateTime();
            if (Math.Abs((after - expectedOccurrence).TotalSeconds) <= OccurrenceToleranceSeconds)
            {
                Dispatcher.UIThread.Invoke(Trigger);
            }
            else
            {
                Logger.LogInformation("cron 触发前对齐校验未通过（预期 {}，实际 {}），已按当前时间重新对齐", expectedOccurrence, after);
            }
        }
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        LoadCron();
    }

    private void LoadCron()
    {
        if (_stopCancellationTokenSource?.IsCancellationRequested == false)
        {
            _stopCancellationTokenSource.Cancel();
        }

        try
        {
            _crontab = Crontab.Parse(Settings.CronExpression);

            var now = ExactTimeService.GetCurrentLocalDateTime();
            Logger.LogInformation("cron 表达式解析成功，下次触发时间：{}", _crontab.GetNextOccurrence(now).ToLongTimeString());
            _stopCancellationTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(CronWorker, TaskCreationOptions.LongRunning);
        }
        catch (TimeCrontabException ex)
        {
            Logger.LogWarning(ex, "cron 表达式解析失败：");
        }
    }

    public override void UnLoaded()
    {
        Settings.PropertyChanged -= SettingsOnPropertyChanged;
        _stopCancellationTokenSource?.Cancel();
    }
}
