using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace ClassFabric.Services.Automation.PlatformTools;

/// <summary>
/// 主屏截图，保存为 PNG。
/// </summary>
public class ScreenCaptureService
{
    public virtual string CapturePrimaryScreen(string saveDirectory)
    {
        Directory.CreateDirectory(saveDirectory);
        var path = Path.Combine(saveDirectory, $"ClassFabric_Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        var width = NativeInterop.GetSystemMetrics(0);  // SM_CXSCREEN
        var height = NativeInterop.GetSystemMetrics(1); // SM_CYSCREEN
        using var bitmap = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(0, 0, 0, 0, new Size(width, height));
        }
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }
}
