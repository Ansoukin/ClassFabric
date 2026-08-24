using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Shared;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Controls.VerticalSchedule;

/// <summary>
/// 竖版课表明暗规则：结束时间不晚于当前时间的项降透明，当前节与未来节全亮。
/// 分隔项按其开始时间参与判定。当前时间点作为绑定参数传入，跨节时触发整列重算。
/// </summary>
public class VerticalLessonFadingMultiConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // 传入参数：
        // [0]: TimeLayoutItem   本项
        // [1]: TimeLayoutItem?  当前时间点（仅作变化触发源）
        if (values.Count < 2 || values[0] is not TimeLayoutItem item)
        {
            return false;
        }

        var end = item.TimeType == 2 ? item.StartTime : item.EndTime;
        var now = IAppHost.GetService<IExactTimeService>().GetCurrentLocalDateTime().TimeOfDay;
        return end <= now;
    }
}
