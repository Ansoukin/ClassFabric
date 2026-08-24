using System.Threading.Tasks;
using ClassFabric.Core;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.app.restart", "重启 ClassFabric", "\ue0bd", addDefaultToMenu:false)]
public class AppRestartAction : ActionBase<AppRestartActionSettings>
{
    protected override async Task OnInvoke()
    {
        AppBase.Current.Restart(Settings.Value);
    }
}