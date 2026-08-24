using System.IO;
using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.deletePath", "删除文件或文件夹", "\uE74D")]
public class DeletePathAction : ActionBase<DeletePathActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var path = Settings.Path;
        if (string.IsNullOrEmpty(path)) return;

        // 路径不存在时静默返回，不视为行动错误。
        if (Directory.Exists(path))
            Directory.Delete(path, Settings.Recursive);
        else if (File.Exists(path))
            File.Delete(path);
    }
}
