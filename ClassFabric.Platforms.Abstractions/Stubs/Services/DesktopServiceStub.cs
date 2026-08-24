using ClassFabric.Platforms.Abstraction.Services;

namespace ClassFabric.Platforms.Abstraction.Stubs.Services;

public class DesktopServiceStub : IDesktopService
{
    /// <inheritdoc />
    public bool IsAutoStartEnabled
    {
        get => false;
        set { }
    }

    /// <inheritdoc />
    public bool IsUrlSchemeRegistered
    {
        get => false;
        set { }
    }
}