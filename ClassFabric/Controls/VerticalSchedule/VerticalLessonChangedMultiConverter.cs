using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Controls.VerticalSchedule;

/// <summary>
/// 判定本项对应的课程是否发生临时换课。上课项序号到 Classes 索引的对齐算法
/// 复制自首字转换器（IndexOf＋TimeType==0 计数），返回对应 ClassInfo 的 IsChangedClass。
/// </summary>
public class VerticalLessonChangedMultiConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // 传入参数：
        // [0]: ClassPlan        当前课表
        // [1]: TimeLayoutItem   本项
        // [2]: int              内容刷新令牌（仅作变化触发源）
        if (values.Count < 3 ||
            values[0] is not ClassPlan classPlan ||
            values[1] is not TimeLayoutItem item)
        {
            return false;
        }

        var layouts = classPlan.TimeLayout?.Layouts;
        if (layouts == null)
        {
            return false;
        }

        var index = layouts.IndexOf(item);
        if (index < 0)
        {
            return false;
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
            return false;
        }

        return classPlan.Classes[classIndex].IsChangedClass;
    }
}
