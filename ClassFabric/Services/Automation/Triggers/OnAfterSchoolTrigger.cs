using System;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Attributes;

namespace ClassFabric.Services.Automation.Triggers;

[TriggerInfo("classfabric.lessons.onAfterSchool", "放学时", "\ued35")]
public class OnAfterSchoolTrigger(ILessonsService lessonsService) : TriggerBase
{
    private ILessonsService LessonsService { get; } = lessonsService;

    public override void Loaded()
    {
        LessonsService.OnAfterSchool += OnLessonsServiceOnAfterSchool;
    }
    public override void UnLoaded()
    {
        LessonsService.OnAfterSchool -= OnLessonsServiceOnAfterSchool;
    }

    private void OnLessonsServiceOnAfterSchool(object? sender, EventArgs e)
    {
        Trigger();
    }
}