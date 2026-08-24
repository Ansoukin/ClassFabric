using ClassFabric.Core.Enums;

namespace ClassFabric.Core.Attributes;

/// <summary>
/// 附加设置控件用法。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class AttachedSettingsUsage(AttachedSettingsTargets targets) : Attribute
{
    /// <summary>
    /// 用法。
    /// </summary>
    public AttachedSettingsTargets Targets { get; } = targets;
}