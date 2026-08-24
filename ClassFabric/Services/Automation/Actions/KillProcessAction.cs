using System.Diagnostics;
using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.killProcess", "退出进程", "\uE9D5")]
public class KillProcessAction : ActionBase<KillProcessActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var processName = Settings.ProcessName;
        if (string.IsNullOrWhiteSpace(processName)) return;

        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                // 只请求主窗口温和关闭，不做强杀；进程已退出或无窗口等失败情形直接跳过。
                try
                {
                    process.CloseMainWindow();
                }
                catch
                {
                }
            }
        }
    }
}
