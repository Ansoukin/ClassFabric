using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Controls.TimeLine;

public class TimeLineListControlDataTemplateSelector : IDataTemplate
{
    [Content]
    public Dictionary<string, IDataTemplate> AvailableTemplates { get; } = new Dictionary<string, IDataTemplate>();

    public Control? Build(object? param)
    {
        if (param is not TimeLayoutItem o)
        {
            return null;
        }
        var key = o.TimeType switch
        {
            0 => "DataTemplateTimePoint",
            1 => "DataTemplateTimePoint",
            2 => "DataTemplateSeparator",
            3 => "DataTemplateAction",
            _ => "DataTemplateTimePoint"
        };
        return AvailableTemplates.GetValueOrDefault(key)?.Build(param);
    }

    public bool Match(object? data)
    {
        return data is TimeLayoutItem;
    }
}
