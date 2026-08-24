using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Attributes;
using ClassFabric.Core.Models.Automation;
using ClassFabric.Models.Actions;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.toggleWorkflow", "开关自动化方案", "\uE8D4")]
public class ToggleWorkflowAction : ActionBase<ToggleWorkflowActionSettings>
{
    // 行动提供方实例每次执行都会重建，恢复所需的切换前状态必须放在静态集合中，以行动组 Guid 为键。
    static readonly ConcurrentDictionary<Guid, bool> StateBeforeInvoke = new();

    IAutomationService AutomationService =>
        IAppHost.Host.Services.GetRequiredService<IAutomationService>();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        if (Settings.TargetProfileId == Guid.Empty) return;

        // Workflows 的加载与替换都发生在 UI 线程，查找与改写保持在同一线程避免竞态。
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var workflow = FindTargetWorkflow();
            if (workflow == null) return;

            var current = workflow.ActionSet.IsEnabled;
            var target = ResolveTargetStatus(current);
            if (target == current) return;

            StateBeforeInvoke[ActionSet.Guid] = current;
            workflow.ActionSet.IsEnabled = target;
        });
    }

    protected override async Task OnRevert()
    {
        await base.OnRevert();
        if (!StateBeforeInvoke.TryRemove(ActionSet.Guid, out var restored)) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var workflow = FindTargetWorkflow();
            if (workflow != null)
                workflow.ActionSet.IsEnabled = restored;
        });
    }

    Workflow? FindTargetWorkflow() =>
        AutomationService.Workflows.FirstOrDefault(x => x.ActionSet.Guid == Settings.TargetProfileId);

    bool ResolveTargetStatus(bool current)
    {
        if (string.Equals(Settings.Mode, ToggleWorkflowActionSettings.ModeEnable, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(Settings.Mode, ToggleWorkflowActionSettings.ModeDisable, StringComparison.OrdinalIgnoreCase))
            return false;
        return !current;
    }
}
