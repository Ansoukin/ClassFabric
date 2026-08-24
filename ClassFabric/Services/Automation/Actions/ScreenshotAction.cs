using System;
using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Services.Automation.PlatformTools;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.screenshot", "屏幕截图", "\uE722")]
public class ScreenshotAction : ActionBase<ScreenshotActionSettings>
{
    private ScreenCaptureService ScreenCaptureService =>
        IAppHost.Host.Services.GetRequiredService<ScreenCaptureService>();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        // 设置留空时回退到系统图片库，避免把截图散落到当前工作目录。
        var saveDirectory = Settings.SaveDirectory;
        if (string.IsNullOrWhiteSpace(saveDirectory))
            saveDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        ScreenCaptureService.CapturePrimaryScreen(saveDirectory);
    }
}
