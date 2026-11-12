using System;
using System.Threading;
using System.Timers;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Models.Automation.Triggers;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Triggers;

[AutomationCategory("事件与集成")]
[TriggerInfo("SystemTools.LongIdleTrigger", "长时间未操作电脑时", FluentIcons.DesktopCursorRegular)]
public class LongIdleTrigger : TriggerBase<LongIdleTriggerSettings>
{
    ILogger<LongIdleTrigger> Logger { get; } = App.GetService<ILogger<LongIdleTrigger>>();

    private System.Timers.Timer? _timer;
    private int _hasTriggered;

    public override void Loaded()
    {
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    public override void UnLoaded()
    {
        if (_timer == null)
        {
            return;
        }

        _timer.Elapsed -= OnTimerElapsed;
        _timer.Stop();
        _timer.Dispose();
        _timer = null;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            var idle = TimeSpan.FromMilliseconds(PlatformServices.SystemInputService.GetIdleMilliseconds());
            var threshold = TimeSpan.FromMinutes(Math.Max(1, Settings.IdleMinutes));

            if (idle >= threshold)
            {
                // 空闲期间只触发一次，恢复操作后重新武装
                if (Interlocked.CompareExchange(ref _hasTriggered, 1, 0) != 0)
                {
                    return;
                }

                Logger.LogInformation("超过未操作阈值，触发自动化。Idle={Idle}, Threshold={Threshold}", idle, threshold);
                Trigger();
                return;
            }

            if (Interlocked.CompareExchange(ref _hasTriggered, 0, 1) == 0)
            {
                return;
            }

            if (AssociatedWorkflow?.ActionSet?.IsRevertEnabled == true &&
                AssociatedWorkflow.ActionSet.Status == ActionSetStatus.IsOn)
            {
                Logger.LogInformation("检测到用户恢复操作，触发恢复。Idle={Idle}", idle);
                TriggerRevert();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "检测用户空闲状态时出错");
        }
    }
}
