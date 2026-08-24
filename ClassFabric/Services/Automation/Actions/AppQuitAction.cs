using System.Threading.Tasks;
using ClassFabric.Core;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.app.quit", "退出 ClassFabric", "\ue0df", addDefaultToMenu:false)]
public class AppQuitAction : ActionBase
{
    protected override async Task OnInvoke()
    {
        AppBase.Current.Stop();
    }
}