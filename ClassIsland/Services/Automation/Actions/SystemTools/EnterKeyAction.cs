using System;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Platforms.Abstraction;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 「按下 Enter 键」行动。
/// </summary>
[AutomationCategory("模拟操作")]
[ActionInfo("SystemTools.EnterKey", "按下 Enter 键", FluentIcons.KeyboardRegular, false)]
public class EnterKeyAction(ILogger<EnterKeyAction> logger) : ActionBase
{
    private readonly ILogger<EnterKeyAction> _logger = logger;

    private const ushort VkReturn = 0x0D; // Enter 键的虚拟键码
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

            _logger.LogInformation("正在模拟按下 Enter 键");

            PlatformServices.SystemInputService.KeyDown(VkReturn);
            // 短暂延迟确保按键被注册
            await Task.Delay(KeyPressDelay);
            PlatformServices.SystemInputService.KeyUp(VkReturn);

            _logger.LogInformation("Enter 键已成功发送");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送 Enter 键失败");
            throw; // 让框架记录错误
        }

        await base.OnInvoke();
    }
}
