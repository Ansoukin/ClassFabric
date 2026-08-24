using System.Collections.ObjectModel;
using ClassFabric.Core.Attributes;
using ClassFabric.Shared;
using ClassFabric.Shared.Interfaces;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Core.Abstractions.Services;

public interface IAttachedSettingsHostService
{
    /// <summary>
    /// 已注册的附加设置控件信息。
    /// </summary>
    public static ObservableCollection<AttachedSettingsControlInfo> RegisteredControls { get; } = new();

    public static ObservableCollection<AttachedSettingsControlInfo> TimePointSettingsAttachedSettingsControls { get; } =
        new();

    public static ObservableCollection<AttachedSettingsControlInfo>
        TimeLayoutSettingsAttachedSettingsControls { get; } = new();
    public static ObservableCollection<AttachedSettingsControlInfo> ClassPlanSettingsAttachedSettingsControls { get; } = new();
    public static ObservableCollection<AttachedSettingsControlInfo> SubjectSettingsAttachedSettingsControls { get; } = new();
    
    public static T? GetAttachedSettingsByPriority<T>(Guid id, 
        Subject? subject = null,
        TimeLayoutItem? timeLayoutItem = null, 
        ClassPlan? classPlan = null, 
        TimeLayout? timeLayout = null) 
        where T : IAttachedSettings
    {
        var l = new AttachableSettingsObject?[] { subject, timeLayoutItem, classPlan, timeLayout };
        foreach (var i in l)
        {
            if (i == null) continue;
            var o = i.GetAttachedObject<T>(id);
            if (o?.IsAttachSettingsEnabled == true)
            {
                return o;
            }
        }

        return default;
    }
}