using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.AppUpdating.Sources;

/// <summary>
/// 从 GitHub Releases 获取应用更新的更新源。
/// </summary>
internal sealed class GitHubUpdateSource : IUpdateSource, IDisposable
{
    /// <summary>
    /// 正式版通道 ID。
    /// </summary>
    public const string StableChannelId = "stable";

    /// <summary>
    /// 预览版通道 ID。
    /// </summary>
    public const string PreviewChannelId = "preview";

    /// <summary>
    /// 正式版通道在设置中的持久化 GUID。该值固定，用于兼容既有的更新通道下拉框绑定。
    /// </summary>
    public static readonly Guid StableChannelGuid = new("6d1f5c2e-2f6b-4c8e-9d31-2a7f4b0c5e11");

    /// <summary>
    /// 预览版通道在设置中的持久化 GUID。
    /// </summary>
    public static readonly Guid PreviewChannelGuid = new("6d1f5c2e-2f6b-4c8e-9d31-2a7f4b0c5e12");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly TimeSpan CacheFreshness = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RateLimitBackoff = TimeSpan.FromMinutes(10);

    private DateTimeOffset _rateLimitedUntil = DateTimeOffset.MinValue;

    /// <summary>
    /// 初始化一个 GitHub Releases 更新源。
    /// </summary>
    /// <param name="repository">形如 <c>owner/repo</c> 的仓库名。</param>
    /// <param name="cacheDirectory">本地缓存目录。为空时不启用缓存。</param>
    /// <param name="handler">可注入的 HTTP 消息处理器，用于离线测试。</param>
    /// <param name="logger">日志记录器。</param>
    public GitHubUpdateSource(string repository, string? cacheDirectory = null,
        HttpMessageHandler? handler = null, ILogger? logger = null)
    {
        var normalized = (repository ?? string.Empty).Trim().Trim('/');
        Repository = string.IsNullOrWhiteSpace(normalized)
            ? UpdateSourceIds.DefaultGitHubRepository
            : normalized;
        _cacheDirectory = string.IsNullOrWhiteSpace(cacheDirectory) ? null : cacheDirectory;
        Logger = logger;
        _httpClient = handler == null ? new HttpClient() : new HttpClient(handler, false);
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ClassFabric", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    /// <summary>
    /// 当前使用的 GitHub 仓库。
    /// </summary>
    public string Repository { get; }

    /// <summary>
    /// 当前使用的缓存目录，未启用缓存时为 <c>null</c>。
    /// </summary>
    private readonly string? _cacheDirectory;

    private readonly HttpClient _httpClient;

    private ILogger? Logger { get; }

    /// <inheritdoc />
    public UpdateSourceKind Kind => UpdateSourceKind.GitHub;

    /// <inheritdoc />
    public string DisplayName => "GitHub Releases";

    /// <summary>
    /// 将持久化的通道 GUID 转换为更新源通道 ID。
    /// </summary>
    public static string GetChannelIdFromGuid(Guid channelGuid) =>
        channelGuid == PreviewChannelGuid ? PreviewChannelId : StableChannelId;

    /// <summary>
    /// 将更新源通道 ID 转换为持久化的通道 GUID。
    /// </summary>
    public static Guid GetChannelGuid(string? channelId) =>
        string.Equals(channelId, PreviewChannelId, StringComparison.OrdinalIgnoreCase)
            ? PreviewChannelGuid
            : StableChannelGuid;

    /// <summary>
    /// 判断某个持久化通道 GUID 是否属于 GitHub 更新源。
    /// </summary>
    public static bool IsGitHubChannelGuid(Guid channelGuid) =>
        channelGuid == StableChannelGuid || channelGuid == PreviewChannelGuid;

    /// <summary>
    /// 按当前安装形态计算 GitHub Release 中应当存在的更新包文件名。
    /// </summary>
    /// <param name="packagingType">安装形态。</param>
    /// <param name="subChannel">子频道。</param>
    /// <returns>期望的资产名；当前安装形态不支持时返回 <c>null</c>。</returns>
    public static string? GetExpectedAssetName(string packagingType, string subChannel) => packagingType switch
    {
        "installer" => $"ClassFabric_{subChannel}.exe",
        "folder" or "folderClassic" => $"ClassFabric_app_{subChannel}.zip",
        _ => null
    };

    /// <summary>
    /// 构造 GitHub 更新源的通道列表。该操作不需要网络请求。
    /// </summary>
    public static UpdateSourceChannels CreateChannels() => new(
    [
        new(StableChannelId, "正式版", "仅获取 GitHub 上标记为正式发布的版本。"),
        new(PreviewChannelId, "预览版", "包含 GitHub 上标记为预发布的版本，可能不稳定，请谨慎使用。")
    ], StableChannelId);

    /// <inheritdoc />
    public Task<UpdateSourceChannels> GetChannelsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateChannels());

    /// <inheritdoc />
    public async Task<UpdateRelease?> GetLatestReleaseAsync(
        UpdateSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        var allowPrerelease = IsPreviewChannel(request.ChannelId);
        var releases = await GetReleasesAsync(request.ForceRefresh, cancellationToken);

        var candidate = releases
            .Where(x => !x.Draft && !string.IsNullOrWhiteSpace(x.TagName))
            .Select(x => (Release: x, Version: ParseVersion(x.TagName)))
            .Where(x => x.Version != null)
            .Where(x => allowPrerelease || !x.Release.Prerelease)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.Release.PublishedAt ?? DateTimeOffset.MinValue)
            .FirstOrDefault();

        if (candidate.Release == null)
        {
            return null;
        }

        var expectedAssetName = GetExpectedAssetName(request.PackagingType, request.SubChannel);
        if (expectedAssetName == null)
        {
            throw new InvalidOperationException(
                $"当前安装形态（{request.PackagingType}）不支持通过 GitHub Releases 更新。");
        }

        var asset = candidate.Release.Assets.FirstOrDefault(x =>
            string.Equals(x.Name, expectedAssetName, StringComparison.OrdinalIgnoreCase));
        if (asset == null)
        {
            var available = candidate.Release.Assets.Length == 0
                ? "该版本未提供任何下载资产"
                : $"该版本提供的资产：{string.Join("、", candidate.Release.Assets.Select(x => x.Name))}";
            throw new InvalidOperationException(
                $"GitHub Release {candidate.Release.TagName} 中没有与当前安装形态匹配的更新包" +
                $"（期望 {expectedAssetName}）。{available}。");
        }

        Logger?.LogInformation("GitHub 更新源命中版本 {Tag}，更新包 {Asset}", candidate.Release.TagName, asset.Name);
        return new UpdateRelease(
            candidate.Version!.ToString(),
            NormalizeTag(candidate.Release.TagName),
            candidate.Release.Body ?? string.Empty,
            candidate.Release.PublishedAt,
            [new UpdatePackageAsset(asset.Name, asset.DownloadUri, asset.Size)],
            NativeMetadata: candidate.Release);
    }

    private static bool IsPreviewChannel(string? channelId) =>
        string.Equals(channelId, PreviewChannelId, StringComparison.OrdinalIgnoreCase) ||
        (Guid.TryParse(channelId, out var guid) && guid == PreviewChannelGuid);

    private static string NormalizeTag(string tagName) => tagName.Trim().TrimStart('v', 'V');

    private static Version? ParseVersion(string tagName)
    {
        var normalized = NormalizeTag(tagName);
        var separator = normalized.IndexOfAny(['-', '+']);
        if (separator >= 0)
        {
            normalized = normalized[..separator];
        }

        return Version.TryParse(normalized, out var version) ? version : null;
    }

    private async Task<GitHubRelease[]> GetReleasesAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var cachedJson = await ReadCacheAsync(cancellationToken);
        if (!forceRefresh && cachedJson != null && !IsCacheExpired())
        {
            return DeserializeReleases(cachedJson);
        }

        if (DateTimeOffset.UtcNow < _rateLimitedUntil)
        {
            if (cachedJson != null)
            {
                Logger?.LogWarning("GitHub 接口处于限流退避期，改用本地缓存的发行信息。");
                return DeserializeReleases(cachedJson);
            }

            throw new InvalidOperationException(
                $"GitHub 接口仍在限流退避期，请在 {_rateLimitedUntil.ToLocalTime():HH:mm} 后重试。");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{Repository}/releases?per_page=20");
        var etag = await ReadEtagAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (cachedJson != null)
            {
                Logger?.LogWarning(ex, "获取 GitHub 发行信息失败，改用本地缓存的发行信息。");
                return DeserializeReleases(cachedJson);
            }

            throw new InvalidOperationException($"无法连接 GitHub 获取更新信息：{ex.Message}", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotModified && cachedJson != null)
            {
                TouchCache();
                return DeserializeReleases(cachedJson);
            }

            if (IsRateLimited(response.StatusCode))
            {
                _rateLimitedUntil = DateTimeOffset.UtcNow + RateLimitBackoff;
                var message = BuildRateLimitMessage(response);
                if (cachedJson != null)
                {
                    Logger?.LogWarning("{Message} 已改用本地缓存的发行信息。", message);
                    return DeserializeReleases(cachedJson);
                }

                throw new InvalidOperationException(message);
            }

            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode == HttpStatusCode.NotFound
                    ? $"GitHub 上不存在仓库 {Repository}（或该仓库未公开），请检查更新源设置。"
                    : $"GitHub 返回了意外的响应（HTTP {(int)response.StatusCode} {response.ReasonPhrase}）。";
                if (cachedJson != null)
                {
                    Logger?.LogWarning("{Message} 已改用本地缓存的发行信息。", message);
                    return DeserializeReleases(cachedJson);
                }

                throw new InvalidOperationException(message);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var releases = DeserializeReleases(json);
            await WriteCacheAsync(json, response.Headers.ETag?.Tag, cancellationToken);
            return releases;
        }
    }

    private static bool IsRateLimited(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.Forbidden || (int)statusCode == 429;

    private string BuildRateLimitMessage(HttpResponseMessage response)
    {
        var reason = (int)response.StatusCode == 429
            ? "GitHub 接口访问过于频繁（HTTP 429）"
            : "GitHub 拒绝了本次请求（HTTP 403）";
        var remaining = response.Headers.TryGetValues("X-RateLimit-Remaining", out var values)
            ? values.FirstOrDefault()
            : null;
        var hint = remaining == "0" ? "，匿名访问的速率限制为每小时 60 次" : "";
        var retryAfter = response.Headers.RetryAfter?.Delta;
        var retry = retryAfter is { TotalSeconds: > 0 }
            ? $"，请在 {Math.Max(1, Math.Ceiling(retryAfter.Value.TotalMinutes))} 分钟后重试"
            : "，请稍后重试";
        return $"{reason}{hint}（仓库：{Repository}）{retry}。";
    }

    private static GitHubRelease[] DeserializeReleases(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<GitHubRelease[]>(json, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"GitHub 返回的发行信息无法解析：{ex.Message}", ex);
        }
    }

    private string? CacheFilePath => _cacheDirectory == null
        ? null
        : Path.Combine(_cacheDirectory, $"GitHubReleases.{SanitizeRepository(Repository)}.json");

    private string? CacheEtagFilePath => CacheFilePath == null ? null : CacheFilePath + ".etag";

    private static string SanitizeRepository(string repository) =>
        new(repository.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray());

    private bool IsCacheExpired()
    {
        var path = CacheFilePath;
        return path == null || !File.Exists(path) ||
               DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(path) > CacheFreshness;
    }

    private async Task<string?> ReadCacheAsync(CancellationToken cancellationToken)
    {
        var path = CacheFilePath;
        if (path == null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "读取 GitHub 更新缓存失败。");
            return null;
        }
    }

    private async Task<string?> ReadEtagAsync(CancellationToken cancellationToken)
    {
        var path = CacheEtagFilePath;
        if (path == null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "读取 GitHub 更新缓存标记失败。");
            return null;
        }
    }

    private async Task WriteCacheAsync(string json, string? etag, CancellationToken cancellationToken)
    {
        var path = CacheFilePath;
        if (path == null || _cacheDirectory == null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            await File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
            if (!string.IsNullOrWhiteSpace(etag))
            {
                await File.WriteAllTextAsync(CacheEtagFilePath!, etag, Encoding.UTF8, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "写入 GitHub 更新缓存失败。");
        }
    }

    private void TouchCache()
    {
        var path = CacheFilePath;
        if (path == null || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "更新 GitHub 缓存时间戳失败。");
        }
    }

    /// <inheritdoc />
    public void Dispose() => _httpClient.Dispose();

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        string? Body,
        bool Draft,
        bool Prerelease,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
        GitHubAsset[] Assets);

    private sealed record GitHubAsset(
        string Name,
        long Size,
        [property: JsonPropertyName("browser_download_url")] Uri DownloadUri);
}