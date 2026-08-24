using System.Threading.Tasks;
using Avalonia.Threading;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Shared;
using ClassFabric.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.openSettings", "打开设置窗口", "\uE713")]
public class OpenSettingsAction : ActionBase<OpenSettingsActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IAppHost.Host.Services.GetRequiredService<SettingsWindowNew>().Open();
        });
    }
}
