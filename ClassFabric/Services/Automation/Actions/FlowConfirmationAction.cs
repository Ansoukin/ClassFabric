using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassFabric.Core;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Shared;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.flowConfirmation", "行动流执行确认", "\uE7B8")]
public class FlowConfirmationAction : ActionBase<FlowConfirmationActionSettings>
{
    IActionService ActionService =>
        IAppHost.Host.Services.GetRequiredService<IActionService>();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        // ContentDialog 的创建与显示都必须在 UI 线程完成。
        var result = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = "行动流执行确认",
                Content = string.IsNullOrWhiteSpace(Settings.Message)
                    ? "是否继续执行此行动流？"
                    : Settings.Message,
                PrimaryButtonText = "立即继续",
                SecondaryButtonText = Settings.AllowDelay ? "延迟 5 分钟" : null,
                CloseButtonText = "终止",
                DefaultButton = ContentDialogButton.Primary
            };
            return await dialog.ShowAsync(AppBase.Current.GetRootWindow());
        });

        switch (result)
        {
            case ContentDialogResult.Primary:
                return;
            case ContentDialogResult.Secondary when Settings.AllowDelay:
                // 延迟等待期间保持对行动组中断请求的响应。
                await Task.Delay(TimeSpan.FromMinutes(5), InterruptCancellationToken);
                return;
            default:
                // 行动项自身的异常会被服务捕获吞掉，终止整条行动链必须显式中断所在行动组。
                _ = ActionService.InterruptActionSetAsync(ActionSet);
                throw new OperationCanceledException("用户已选择终止行动流。");
        }
    }
}
