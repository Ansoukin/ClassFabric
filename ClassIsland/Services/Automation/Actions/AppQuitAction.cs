using System.Threading.Tasks;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
namespace ClassIsland.Services.Automation.Actions;

[AutomationCategory("应用与系统")]
[ActionInfo("classisland.app.quit", "退出 ClassFabric", "\ue0df", addDefaultToMenu:false)]
public class AppQuitAction : ActionBase
{
    protected override async Task OnInvoke()
    {
        AppBase.Current.Stop();
    }
}
