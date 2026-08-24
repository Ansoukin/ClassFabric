using System.Globalization;
using Avalonia.Data.Converters;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Shared;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Core.Controls.LessonsControls;

public class ClassInfoMultiConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object? Convert(IList<object?> values, Type targetType, object parameter, CultureInfo culture)
    {
        // 输入参数：
        // [0]: TimeLayoutItem            SelectedItem
        // [1]: ClassPlan                 ClassPlan
        if (values.Count < 2)
        {
            return null;
        }

        if (values[0] is not TimeLayoutItem selectedItem ||
            values[1] is not ClassPlan classPlan ||
            classPlan.TimeLayout == null)
        {
            return null;
        }

        var subjectIndex = GetSubjectIndex(classPlan.TimeLayout.Layouts.IndexOf(selectedItem));
        if (subjectIndex >= classPlan.Classes.Count || subjectIndex < 0)
        {
            return null;
        }

        return classPlan.Classes[subjectIndex];

        int GetSubjectIndex(int index)
        {
            if (index == -1)
                return -1;
            var k = classPlan.TimeLayout.Layouts[index];
            var l = classPlan.TimeLayout.Layouts.Where(t => t.TimeType == 0).ToList();
            var i = l.IndexOf(k);
            return i;
        }
    }

    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return [];
    }
}