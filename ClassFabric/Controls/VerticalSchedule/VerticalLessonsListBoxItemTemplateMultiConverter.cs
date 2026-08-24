using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.Templates;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Controls.VerticalSchedule;

/// <summary>
/// 竖版课表项模板选择转换器。判定逻辑复制自横条 LessonsListBoxItemTemplateMultiConverter
/// （internal 不可跨程序集引用），差异：已结束节不再隐藏（改由单元格明暗规则表达），
/// 所有课程项统一渲染为盲字单元格，不再区分展开/收起模板。
/// </summary>
public class VerticalLessonsListBoxItemTemplateMultiConverter : AvaloniaObject, IMultiValueConverter
{
    public static readonly StyledProperty<DataTemplate> CellDataTemplateProperty =
        AvaloniaProperty.Register<VerticalLessonsListBoxItemTemplateMultiConverter, DataTemplate>(
            nameof(CellDataTemplate));

    public DataTemplate CellDataTemplate
    {
        get => GetValue(CellDataTemplateProperty);
        set => SetValue(CellDataTemplateProperty, value);
    }

    public static readonly StyledProperty<DataTemplate> SeparatorDataTemplateProperty =
        AvaloniaProperty.Register<VerticalLessonsListBoxItemTemplateMultiConverter, DataTemplate>(
            nameof(SeparatorDataTemplate));

    public DataTemplate SeparatorDataTemplate
    {
        get => GetValue(SeparatorDataTemplateProperty);
        set => SetValue(SeparatorDataTemplateProperty, value);
    }

    public DataTemplate BlankDataTemplate { get; } = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // 传入参数：
        // [0]: int              TimeType
        // [1]: bool             IsHideDefault
        // [2]: TimeLayoutItem?  当前时间点
        // [3]: TimeLayoutItem   本项
        // [4]: ICollection      有效时间点集合
        if (values.Count < 5)
            return BlankDataTemplate;
        if (values[0] is not int timeType ||
            values[1] is not bool isHideDefault)
        {
            return BlankDataTemplate;
        }

        // 行动项不参与课表显示
        if (timeType == 3)
        {
            return BlankDataTemplate;
        }

        var currentItem = values[2] as TimeLayoutItem;
        var item = values[3] as TimeLayoutItem;
        if (values[4] is ICollection<TimeLayoutItem> validTimePoints &&
            (item == null || !validTimePoints.Contains(item)))
        {
            return BlankDataTemplate;
        }

        // 分隔项渲染为刻度槽，占一个整格高度
        if (timeType == 2)
        {
            return SeparatorDataTemplate;
        }

        // 课间一律不渲染；默认隐藏项仅在非当前时隐藏；已过节不隐藏，全天显示
        if (timeType == 1)
        {
            return BlankDataTemplate;
        }
        if (isHideDefault && currentItem != item)
        {
            return BlankDataTemplate;
        }

        return CellDataTemplate;
    }
}
