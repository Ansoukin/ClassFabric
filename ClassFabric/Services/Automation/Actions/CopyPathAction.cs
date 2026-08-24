using System.IO;
using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.copyPath", "复制文件或文件夹", "\uE8C8")]
public class CopyPathAction : ActionBase<CopyPathActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var source = Settings.Source;
        var target = Settings.Target;
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return;
        // 目标已存在且未允许覆盖时整体跳过，保持静默不报错。
        if ((File.Exists(target) || Directory.Exists(target)) && !Settings.Overwrite) return;

        if (Directory.Exists(source))
            FileSystemHelper.CopyDirectory(source, target, Settings.Overwrite);
        else
            File.Copy(source, target, Settings.Overwrite);
    }
}
