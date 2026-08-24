using System.Threading.Tasks;
using Avalonia.Threading;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Shared;
using ClassFabric.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.openClassSwap", "打开换课窗口", "\uE8AB")]
public class OpenClassSwapAction : ActionBase<OpenClassSwapActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var lessonsService = IAppHost.Host.Services.GetRequiredService<ILessonsService>();
            if (lessonsService.CurrentClassPlan == null) return;

            // 与主窗口的换课入口一致：以当前课表初始化并作为其对话框显示。
            var window = new ClassChangingWindow
            {
                ClassPlan = lessonsService.CurrentClassPlan
            };
            await window.ShowDialog(IAppHost.Host.Services.GetRequiredService<MainWindow>());
        });
    }
}
