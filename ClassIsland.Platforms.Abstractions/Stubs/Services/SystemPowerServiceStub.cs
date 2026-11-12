using ClassIsland.Platforms.Abstraction.Services;

namespace ClassIsland.Platforms.Abstraction.Stubs.Services;

/// <inheritdoc />
public class SystemPowerServiceStub : ISystemPowerService
{
    /// <inheritdoc />
    public bool Shutdown(bool powerOff) => false;

    /// <inheritdoc />
    public bool Restart() => false;
}
