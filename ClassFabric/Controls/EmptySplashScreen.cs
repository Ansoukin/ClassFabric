using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FluentAvalonia.UI.Windowing;

namespace ClassFabric.Controls;

public class EmptySplashScreen : IApplicationSplashScreen
{
    public async Task RunTasks(CancellationToken cancellationToken)
    {
    }

    public string AppName { get; } = "ClassFabric";

    public IImage AppIcon { get; } =
        new Bitmap(AssetLoader.Open(new Uri("avares://ClassFabric/Assets/AppLogo@384w.png")));
    public object? SplashScreenContent { get; } = null;
    public int MinimumShowTime { get; } = 0;
}