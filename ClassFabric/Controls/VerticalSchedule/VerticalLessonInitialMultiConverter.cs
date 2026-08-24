using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using ClassFabric.Shared.ComponentModels;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Controls.VerticalSchedule;

/// <summary>
/// 产出课程盲字（科目名首字）。上课项序号到 Classes 索引的对齐算法
/// 复制自 LessonControlBase.UpdateClassInfo。
/// </summary>
public class VerticalLessonInitialMultiConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // 传入参数：
        // [0]: ObservableOrderedDictionary<Guid, Subject>  科目表
        // [1]: ClassPlan                            当前课表
        // [2]: TimeLayoutItem                       本项
        // [3]: int                                  内容刷新令牌（仅作变化触发源）
        if (values.Count < 3 ||
            values[0] is not ObservableOrderedDictionary<Guid, Subject> subjects ||
            values[1] is not ClassPlan classPlan ||
            values[2] is not TimeLayoutItem item)
        {
            return Subject.Fallback.Initial;
        }

        var layouts = classPlan.TimeLayout?.Layouts;
        if (layouts == null)
        {
            return Subject.Fallback.Initial;
        }

        var index = layouts.IndexOf(item);
        if (index < 0)
        {
            return Subject.Fallback.Initial;
        }

        // 本项之前（含自身）的上课项数量减一，即本项在 Classes 中的序号
        var classIndex = -1;
        for (var i = 0; i <= index; i++)
        {
            if (layouts[i].TimeType == 0)
            {
                classIndex++;
            }
        }

        if (classIndex < 0 || classIndex >= classPlan.Classes.Count)
        {
            return Subject.Fallback.Initial;
        }

        var subjectId = classPlan.Classes[classIndex].SubjectId;
        return subjects.TryGetValue(subjectId, out var subject) && !string.IsNullOrEmpty(subject.Initial)
            ? subject.Initial
            : Subject.Fallback.Initial;
    }
}
