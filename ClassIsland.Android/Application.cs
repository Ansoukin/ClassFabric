using System;
using System.IO;
using System.Runtime.Versioning;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Media;
using ClassIsland.Android.Services.Platform;
using ClassIsland.Core.Services;
using ClassIsland.Extensions;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Services;

namespace ClassIsland.Android;

[Application]
[SupportedOSPlatform("android24.0")]
public class Application : AvaloniaAndroidApplication<App>
{

    public static Application Instance { get; private set; } = null!;

    protected Application(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
        Instance = this;
    }

    public override void OnCreate()
    {
        // 移动端档案此前放在 files/.config/ClassFabric，先整体改名到标准目录再做任何文件读写；
        // 失败时按新目录首启处理，旧数据原样保留。该迁移仅过渡版本携带，2.1.1.7 起移除。
        try
        {
            var filesPath = FilesDir?.CanonicalPath;
            if (filesPath != null)
            {
                var source = Path.Combine(filesPath, ".config", "ClassFabric");
                var target = Path.Combine(filesPath, ".config", "ClassIsland");
                if (Directory.Exists(source) && !Directory.Exists(target))
                {
                    Directory.Move(source, target);
                }
            }
        }
        catch
        {
            // 迁移失败不阻塞启动。
        }
        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        PlatformServices.AppLifetimeService = new AndroidAppLifetimeService();
        PlatformServices.LauncherService = new LauncherService();
        PlatformServices.DesktopToastService = new DesktopToastService();
        PlatformServices.LocationService = new LocationService();
        PlatformServices.HapticFeedbackService = new HapticFeedbackService();

        var restartParameters = MainActivity.Current?.TryGetTarget(out var mainActivity) == true
            ? mainActivity.Intent?.GetStringArrayExtra(AndroidAppLifetimeService.RestartParametersExtra)
            : null;
        var buildApp = Program.AppEntry(restartParameters is null
            ? ["--mobile"]
            : [.. restartParameters, "--mobile"]);
        var renderingMode = int.TryParse(GlobalStorageService.GetValue("AndroidRenderingMode"), out var value)
            ? value
            : 0;

        return AppBuilder.Configure<App>(() =>
            {
                var app = buildApp();
                app.OperatingSystem = "android";
                return app;
            })
            .UseAndroid()
            .UseOverlayAssetLoader()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = MainWindow.DefaultFontFamilyKey,
                FontFallbacks =
                [
                    new FontFallback
                    {
                        FontFamily = MainWindow.DefaultFontFamily
                    }
                ]
            })
            .With(new AndroidPlatformOptions()
            {
                RenderingMode = BuildRenderingMode(renderingMode)
            })
            .LogToHostSink();
    }

    private static IReadOnlyList<AndroidRenderingMode> BuildRenderingMode(int userValue) => userValue switch
    {
        2 =>
        [
            AndroidRenderingMode.Vulkan,
            AndroidRenderingMode.Egl,
            AndroidRenderingMode.Software
        ],
        3 => [AndroidRenderingMode.Software],
        _ =>
        [
            AndroidRenderingMode.Egl,
            AndroidRenderingMode.Vulkan,
            AndroidRenderingMode.Software
        ]
    };
}
