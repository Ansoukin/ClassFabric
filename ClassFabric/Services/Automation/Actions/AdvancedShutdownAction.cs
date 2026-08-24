using System.Threading.Tasks;
using Avalonia.Threading;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Services.Automation.PlatformTools;
using ClassFabric.Shared;
using ClassFabric.Views.Automation;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.advancedShutdown", "高级计时关机", "\uEB4F")]
public class AdvancedShutdownAction : ActionBase<AdvancedShutdownActionSettings>
{
    private PowerService PowerService =>
        IAppHost.Host.Services.GetRequiredService<PowerService>();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        var seconds = Settings.Seconds;
        PowerService.ScheduleShutdown(seconds);
        // 行动回调可能来自非 UI 线程的触发器，弹窗统一投递到 UI 线程
        Dispatcher.UIThread.Post(() =>
        {
            new AdvancedShutdownWindow(seconds, PowerService).Show();
        });
    }
}
