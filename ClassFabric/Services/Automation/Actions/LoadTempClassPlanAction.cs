using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.loadTempClassPlan", "加载临时课表", "\uE719")]
public class LoadTempClassPlanAction : ActionBase<LoadTempClassPlanActionSettings>
{
    Guid? _previousClassPlanId;

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var profileService = IAppHost.Host.Services.GetRequiredService<IProfileService>();
        var lessonsService = IAppHost.Host.Services.GetRequiredService<ILessonsService>();
        var exactTimeService = IAppHost.Host.Services.GetRequiredService<IExactTimeService>();

        var targetId = Settings.ClassPlanId;
        if (!profileService.Profile.ClassPlans.TryGetValue(targetId, out _))
            throw new System.Collections.Generic.KeyNotFoundException($"找不到课表 {targetId}，无法加载为临时课表。");

        // 记住当前生效的课表，撤销时切回。
        var now = exactTimeService.GetCurrentLocalDateTime();
        lessonsService.GetClassPlanByDate(now, out var currentId);
        _previousClassPlanId = currentId;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            profileService.Profile.TempClassPlanId = targetId;
            profileService.Profile.TempClassPlanSetupTime = now;
        });
    }

    protected override async Task OnRevert()
    {
        await base.OnRevert();

        var profileService = IAppHost.Host.Services.GetRequiredService<IProfileService>();
        var exactTimeService = IAppHost.Host.Services.GetRequiredService<IExactTimeService>();

        var revertId = _previousClassPlanId;
        var now = exactTimeService.GetCurrentLocalDateTime();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            profileService.Profile.TempClassPlanId = revertId;
            profileService.Profile.TempClassPlanSetupTime = now;
        });
    }
}
