using ClassIsland.Platforms.Abstraction.Services;

namespace ClassIsland.Platforms.Abstraction.Stubs.Services;

/// <inheritdoc />
public class SystemInputServiceStub : ISystemInputService
{
    /// <inheritdoc />
    public void KeyDown(ushort virtualKey)
    {
    }

    /// <inheritdoc />
    public void KeyUp(ushort virtualKey)
    {
    }

    /// <inheritdoc />
    public void MouseDown(SystemMouseButton button)
    {
    }

    /// <inheritdoc />
    public void MouseUp(SystemMouseButton button)
    {
    }

    /// <inheritdoc />
    public void MouseWheel(int delta)
    {
    }

    /// <inheritdoc />
    public bool MoveMouseCursor(int x, int y) => false;

    /// <inheritdoc />
    public uint GetIdleMilliseconds() => 0;
}
