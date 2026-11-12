using System;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Platforms.Abstraction;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 「按下 F11 键」行动。
/// </summary>
[AutomationCategory("模拟操作")]
[ActionInfo("SystemTools.F11Key", "按下 F11 键", FluentIcons.KeyboardRegular, false)]
public class F11Action(ILogger<F11Action> logger) : ActionBase
{
    private readonly ILogger<F11Action> _logger = logger;

    private const ushort VkF11 = 0x7A;
    private const int KeyPressDelay = 20;

    protected override async Task OnInvoke()
    {
        try
        {
            // 平台服务在非 Windows 桌面端是空操作，这里直接早退。
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogWarning("当前平台不支持模拟按键");
                return;
            }

            _logger.LogInformation("正在模拟按下 F11 键");

            PlatformServices.SystemInputService.KeyDown(VkF11);
            await Task.Delay(KeyPressDelay);
            PlatformServices.SystemInputService.KeyUp(VkF11);

            _logger.LogInformation("F11 键已成功发送");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送 F11 键失败");
            throw;
        }

        await base.OnInvoke();
    }
}
