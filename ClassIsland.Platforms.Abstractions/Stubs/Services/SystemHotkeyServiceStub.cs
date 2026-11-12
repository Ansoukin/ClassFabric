using ClassIsland.Platforms.Abstraction.Services;

namespace ClassIsland.Platforms.Abstraction.Stubs.Services;

/// <inheritdoc />
public class SystemHotkeyServiceStub : ISystemHotkeyService
{
    /// <inheritdoc />
    public event EventHandler<SystemHotkeyEventArgs>? HotkeyPressed
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public int RegisterHotkey(int modifierKeys, uint virtualKey) => -1;

    /// <inheritdoc />
    public void UnregisterHotkey(int hotkeyId)
    {
    }

    /// <inheritdoc />
    public bool IsHotkeyRegistered(int modifierKeys, uint virtualKey) => false;

    /// <inheritdoc />
    public void SuspendAllHotkeys()
    {
    }

    /// <inheritdoc />
    public void ResumeAllHotkeys()
    {
    }

    /// <inheritdoc />
    public string GetHotkeyDisplay(int modifierKeys, uint virtualKey) => virtualKey.ToString();
}
