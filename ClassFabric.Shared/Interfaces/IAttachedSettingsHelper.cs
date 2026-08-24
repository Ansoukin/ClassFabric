namespace ClassFabric.Shared.Interfaces;

public interface IAttachedSettingsHelper
{
    public AttachableSettingsObject? AttachedTarget
    {
        get;
        set;
    }
}