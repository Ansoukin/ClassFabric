using System.Threading.Tasks;
using Avalonia.Threading;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Shared;
using ClassFabric.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.openProfileEditor", "打开档案编辑窗口", "\uE81C")]
public class OpenProfileEditorAction : ActionBase<OpenProfileEditorActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IAppHost.Host.Services.GetRequiredService<ProfileSettingsWindow>().Open();
        });
    }
}
