using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg;

namespace ClassIsland.Controls;

/// <summary>
/// 网络图控件，兼容位图与 SVG：插件市场 README 的徽章与图表大量是 SVG
/// （shields.io、repobeats 等），AdvancedImage 的加载管线只走 Skia 位图解码，
/// SVG 字节直接抛「Unable to load bitmap from provided data」。
/// 这里自己异步拉字节，按内容探测分叉解码：SVG 走 SvgImage（矢量，实现 IImage），
/// 其余走 Bitmap，两者都是 IImage，Render 里统一绘制。
/// 不用 Svg 控件的 Path 属性：它对 URL 是同步网络加载，会卡 UI 线程。
/// </summary>
public class SvgAwareImage : Control
{
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<SvgAwareImage, string?>(nameof(Source));

    /// <summary>未加载或加载失败时显示的占位图（轮播用它摆骨架图标，徽章行不设则留空）。</summary>
    public static readonly StyledProperty<IImage?> PlaceholderProperty =
        AvaloniaProperty.Register<SvgAwareImage, IImage?>(nameof(Placeholder));

    public static readonly DirectProperty<SvgAwareImage, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<SvgAwareImage, bool>(nameof(IsLoading), o => o._isLoading);

    private static readonly HttpClient Http = CreateSharedClient();

    // 详情页切换插件会反复请求同一批徽章/图片，进程级缓存挡重复下载；
    // 值为 null 表示该 URL 上次加载失败，直接走占位不再重试（防反复打死链）
    private static readonly ConcurrentDictionary<string, IImage?> Cache = new();

    private IImage? _current;
    private bool _isLoading;
    private CancellationTokenSource? _loadCts;

    static SvgAwareImage()
    {
        AffectsRender<SvgAwareImage>(SourceProperty, PlaceholderProperty);
        AffectsMeasure<SvgAwareImage>(SourceProperty, PlaceholderProperty);
    }

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public IImage? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }

    private static HttpClient CreateSharedClient() => new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    {
        // 徽章图多且小，详情页切换频繁；超时收紧避免死链拖住整个页面
        Timeout = TimeSpan.FromSeconds(15)
    };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            var url = change.GetNewValue<string?>();
            _current = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                CancelLoad();
            }
            else
            {
                _ = LoadAsync(url);
            }
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelLoad();
    }

    private void CancelLoad()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }

    private async Task LoadAsync(string url)
    {
        CancelLoad();
        var cts = _loadCts = new CancellationTokenSource();
        var token = cts.Token;

        IsLoading = true;
        try
        {
            if (Cache.TryGetValue(url, out var cached))
            {
                Apply(cached);
                return;
            }

            // 小延迟让快速划过的请求有机会被取消，解码本身不可取消
            await Task.Delay(10, token);

            var bytes = await Http.GetByteArrayAsync(url, token);
            var image = Decode(bytes);
            Cache[url] = image;
            if (token.IsCancellationRequested)
            {
                return;
            }
            Apply(image);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 换图/卸载导致的取消是正常路径
        }
        catch (Exception e)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?
                .Log(this, "图片加载失败 {Url}: {Message}", url, e.Message);
            Cache[url] = null;
            Apply(null);
        }
        finally
        {
            if (_loadCts == cts)
            {
                IsLoading = false;
            }
        }
    }

    private void Apply(IImage? image)
    {
        _current = image;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>内容探测分叉解码：像 SVG 但解析失败（截断/不合规）再按位图试一次。</summary>
    private static IImage? Decode(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        if (LooksLikeSvg(bytes))
        {
            try
            {
                using var stream = new MemoryStream(bytes);
                var svgSource = SvgSource.Load(stream, null);
                return new SvgImage { Source = svgSource };
            }
            catch
            {
                // 落到下面的位图尝试
            }
        }

        try
        {
            return new Bitmap(new MemoryStream(bytes));
        }
        catch
        {
            return null;
        }
    }

    private static bool LooksLikeSvg(byte[] bytes)
    {
        var start = bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        if (start + 5 <= bytes.Length)
        {
            var head = Encoding.ASCII.GetString(bytes, start, 5);
            if (head.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
                head.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                head.StartsWith("<!DOC", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var probe = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512));
        return probe.Contains("<svg", StringComparison.OrdinalIgnoreCase) ||
               probe.Contains("svg+xml", StringComparison.OrdinalIgnoreCase);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var source = _current ?? Placeholder;
        if (source is null)
        {
            return default;
        }

        var sourceSize = source.Size;
        if (double.IsInfinity(availableSize.Width) && double.IsInfinity(availableSize.Height))
        {
            // 无约束容器里按原生尺寸呈现，避免 ∞ 参与缩放算出 NaN
            return sourceSize;
        }

        // 徽章/轮播都是高度约束场景，Uniform 缩放并居中，与原 AdvancedImage 行为一致
        return Stretch.Uniform.CalculateSize(availableSize, sourceSize);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var source = _current ?? Placeholder;
        if (source is null)
        {
            return;
        }

        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0 || source.Size.Width <= 0 || source.Size.Height <= 0)
        {
            return;
        }
        var destSize = Stretch.Uniform.CalculateSize(bounds.Size, source.Size);
        var destRect = new Rect(
            (bounds.Width - destSize.Width) / 2,
            (bounds.Height - destSize.Height) / 2,
            destSize.Width,
            destSize.Height);
        source.Draw(context, new Rect(source.Size), destRect);
    }
}
