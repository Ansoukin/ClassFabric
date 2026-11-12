using ClassIsland.Platforms.Abstraction.Services;

namespace ClassIsland.Platforms.Abstraction.Stubs.Services;

/// <inheritdoc />
public class SystemEnvironmentServiceStub : ISystemEnvironmentService
{
    /// <inheritdoc />
    public bool SetWallpaperImage(string imagePath, int fitStyle) => false;

    /// <inheritdoc />
    public bool SetWallpaperSolidColor(byte r, byte g, byte b) => false;

    /// <inheritdoc />
    public bool SetSystemTheme(bool light) => false;

    /// <inheritdoc />
    public AccentColorSnapshot CaptureAccentColor() => new();

    /// <inheritdoc />
    public bool ApplyAccentColor(AccentColorRgb color) => false;

    /// <inheritdoc />
    public bool RestoreAccentColor(AccentColorSnapshot snapshot) => false;

    /// <inheritdoc />
    public bool SetSystemVolume(float percent, bool? mute) => false;

    /// <inheritdoc />
    public bool CaptureScreenToPng(string filePath) => false;

    /// <inheritdoc />
    public bool OperateForegroundWindow(ForegroundWindowOperation operation) => false;
}
