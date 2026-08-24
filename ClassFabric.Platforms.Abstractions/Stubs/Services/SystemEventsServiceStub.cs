using ClassFabric.Platforms.Abstraction.Services;

namespace ClassFabric.Platforms.Abstraction.Stubs.Services;

/// <inheritdoc />
public class SystemEventsServiceStub : ISystemEventsService
{
    /// <inheritdoc />
    public event EventHandler? TimeChanged;
}