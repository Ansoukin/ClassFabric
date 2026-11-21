using System;

namespace ClassIsland.Services.AppUpdating.Sources;

/// <summary>
/// 应用更新源 ID，以及它们在设置界面中的呈现顺序。
/// </summary>
internal static class UpdateSourceIds
{
    /// <summary>
    /// ClassIsland 分发中心（PDCC）更新源 ID。
    /// </summary>
    public const string PhainonDistributionCenter = "pdcc";

    /// <summary>
    /// GitHub Releases 更新源 ID。
    /// </summary>
    public const string GitHub = "github";

    /// <summary>
    /// Gitee Releases 更新源 ID。
    /// </summary>
    public const string Gitee = "gitee";

    /// <summary>
    /// 默认的 GitHub 更新仓库。
    /// </summary>
    public const string DefaultGitHubRepository = "Ansoukin/ClassFabric";

    /// <summary>
    /// 默认的 Gitee 更新仓库。
    /// </summary>
    public const string DefaultGiteeRepository = "ansoukin/ClassFabric";

    /// <summary>
    /// 默认更新源 ID。
    /// </summary>
    public const string Default = GitHub;

    private static readonly string[] Order =
        [GitHub, Gitee, PhainonDistributionCenter];

    /// <summary>
    /// 将任意设置值规范化到受支持的更新源 ID。已废弃的 <c>github+pdcc</c>（GitHub 主源回退分发中心）
    /// 旧设置值不是合法 ID，会被归一为默认的 GitHub 源。
    /// </summary>
    public static string Normalize(string? id) => Array.IndexOf(Order, id) >= 0 ? id! : Default;

    /// <summary>
    /// 获取更新源在设置界面中的索引。
    /// </summary>
    public static int GetIndex(string? id) => Math.Max(0, Array.IndexOf(Order, Normalize(id)));

    /// <summary>
    /// 由设置界面索引获取更新源 ID。
    /// </summary>
    public static string FromIndex(int index) =>
        index >= 0 && index < Order.Length ? Order[index] : Default;
}