using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Controls.VerticalSchedule;

/// <summary>
/// 判断单元格数据项是否等于指定锚点项（当前时间点/首个可渲染项/末个可渲染项），
/// 用于驱动单元格伪类。锚点经绑定传入，不引入新的数据源。
/// </summary>
public class VerticalLessonItemMatchMultiConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // 传入参数：
        // [0]: TimeLayoutItem       本项
        // [1]: TimeLayoutItem?      锚点项
        if (values.Count < 2)
        {
            return false;
        }

        return values[0] is TimeLayoutItem item &&
               values[1] is TimeLayoutItem anchor &&
               ReferenceEquals(item, anchor);
    }
}
