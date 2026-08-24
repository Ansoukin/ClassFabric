using System.IO;
using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.movePath", "移动文件或文件夹", "\uE8DE")]
public class MovePathAction : ActionBase<MovePathActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var settings = Settings;
        if (string.IsNullOrEmpty(settings.Source) || string.IsNullOrEmpty(settings.Target)) return;
        // 目标已存在且未允许覆盖时整体跳过，与复制行动保持一致语义。
        if ((File.Exists(settings.Target) || Directory.Exists(settings.Target)) && !settings.Overwrite) return;

        // 先复制成功后再删源，复制中途失败时保留源数据。
        if (Directory.Exists(settings.Source))
        {
            FileSystemHelper.CopyDirectory(settings.Source, settings.Target, settings.Overwrite);
            Directory.Delete(settings.Source, true);
        }
        else
        {
            File.Copy(settings.Source, settings.Target, settings.Overwrite);
            File.Delete(settings.Source);
        }
    }
}
