using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.AppUpdating.Sources;

/// <summary>
/// 从 Gitee Releases 获取应用更新的更新源。Gitee v5 接口沿用 GitHub Releases 的请求路径与响应结构，
/// 但返回的字段更精简：发行版可能没有 <c>published_at</c> 与 <c>draft</c>，附件可能没有 <c>size</c>，
/// 共用的解析逻辑对这些缺失字段都能容忍。
/// </summary>
internal sealed class GiteeUpdateSource : GitHubUpdateSource
{
    /// <summary>
    /// 初始化一个 Gitee Releases 更新源。
    /// </summary>
    /// <param name="cacheDirectory">本地缓存目录。为空时不启用缓存。</param>
    /// <param name="handler">可注入的 HTTP 消息处理器，用于离线测试。</param>
    /// <param name="logger">日志记录器。</param>
    public GiteeUpdateSource(string? cacheDirectory = null,
        HttpMessageHandler? handler = null, ILogger? logger = null)
        : base(UpdateSourceIds.DefaultGiteeRepository, cacheDirectory, handler, logger)
    {
    }

    /// <inheritdoc />
    public override UpdateSourceKind Kind => UpdateSourceKind.Gitee;

    /// <inheritdoc />
    public override string DisplayName => "Gitee Releases";

    /// <inheritdoc />
    protected override string HostLabel => "Gitee";

    /// <inheritdoc />
    protected override string ApiBaseUrl => "https://gitee.com/api/v5/";

    /// <inheritdoc />
    // Gitee 接口不认 GitHub 专属的版本化 Accept 头，不发。
    protected override string? AcceptHeader => null;

    /// <inheritdoc />
    protected override string CacheFilePrefix => "GiteeReleases";

    /// <inheritdoc />
    // Gitee 没有公开的匿名访问配额数字，限流提示里不写死。
    protected override string AnonymousRateLimitHint => "";
}
