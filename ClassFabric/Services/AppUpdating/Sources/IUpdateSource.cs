using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassIsland.Services.AppUpdating.Sources;

/// <summary>
/// 更新源的种类。
/// </summary>
internal enum UpdateSourceKind
{
    /// <summary>
    /// ClassIsland 分发中心（PDCC / S3），兼容保留。
    /// </summary>
    PhainonDistributionCenter,

    /// <summary>
    /// GitHub Releases。
    /// </summary>
    GitHub
}

/// <summary>
/// 更新源提供的一个更新通道。
/// </summary>
/// <param name="Id">通道 ID。PDCC 通道为服务端下发的 GUID，GitHub 通道为固定的字符串 ID。</param>
/// <param name="Name">通道名称。</param>
/// <param name="Description">通道说明。</param>
internal sealed record UpdateSourceChannel(string Id, string Name, string Description = "");

/// <summary>
/// 更新源提供的通道集合。
/// </summary>
/// <param name="Channels">所有可用通道。</param>
/// <param name="DefaultChannelId">默认通道 ID。</param>
internal sealed record UpdateSourceChannels(IReadOnlyList<UpdateSourceChannel> Channels, string? DefaultChannelId);

/// <summary>
/// 更新源提供的一个可供下载的更新包。
/// </summary>
/// <param name="Name">文件名。</param>
/// <param name="DownloadUri">下载地址。</param>
/// <param name="Size">文件大小（字节）。</param>
/// <param name="Sha512">可选的 SHA512 摘要（十六进制）。</param>
internal sealed record UpdatePackageAsset(string Name, Uri DownloadUri, long Size = 0, string? Sha512 = null);

/// <summary>
/// 更新源提供的一个可用版本。
/// </summary>
/// <param name="Version">版本号，如 2.2.0.0。</param>
/// <param name="FriendlyVersion">友好版本号，用于界面展示。</param>
/// <param name="ChangeLog">更新日志（Markdown）。</param>
/// <param name="PublishedAt">发布时间。</param>
/// <param name="Assets">该版本中与当前安装形态匹配的更新包。</param>
/// <param name="FileMapJson">PDCC 文件图，仅 PDCC 更新源会填充。</param>
/// <param name="FileMapSignature">PDCC 文件图签名，仅 PDCC 更新源会填充。</param>
/// <param name="NativeMetadata">更新源自身的原始元数据。</param>
internal sealed record UpdateRelease(
    string Version,
    string FriendlyVersion,
    string ChangeLog,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<UpdatePackageAsset> Assets,
    string? FileMapJson = null,
    string? FileMapSignature = null,
    object? NativeMetadata = null);

/// <summary>
/// 一次更新查询的输入参数。
/// </summary>
/// <param name="ChannelId">要查询的通道 ID。</param>
/// <param name="SubChannel">当前子频道，即 <c>AppBase.Current.AppSubChannel</c>。</param>
/// <param name="PackagingType">当前安装形态。</param>
/// <param name="CurrentVersion">当前应用版本。</param>
/// <param name="ForceRefresh">是否忽略本地缓存强制刷新。</param>
internal sealed record UpdateSourceRequest(
    string ChannelId,
    string SubChannel,
    string PackagingType,
    string CurrentVersion,
    bool ForceRefresh = false);

/// <summary>
/// 应用更新源。更新源负责提供可用通道与最新版本，不负责下载与部署。
/// </summary>
internal interface IUpdateSource
{
    /// <summary>
    /// 更新源种类。
    /// </summary>
    UpdateSourceKind Kind { get; }

    /// <summary>
    /// 供界面展示的更新源名称。
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 获取该更新源提供的所有通道。
    /// </summary>
    Task<UpdateSourceChannels> GetChannelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取符合请求条件的最新版本。若没有比当前更新的版本，返回 <c>null</c>。
    /// </summary>
    Task<UpdateRelease?> GetLatestReleaseAsync(
        UpdateSourceRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 记录最近一次检查更新得到的待处理更新，供下载与部署阶段判断更新来源。
/// </summary>
/// <param name="SourceId">产生该更新的更新源 ID。</param>
/// <param name="Version">目标版本号。</param>
/// <param name="SubChannel">目标子频道。</param>
/// <param name="PackagingType">目标安装形态。</param>
/// <param name="AssetName">更新包文件名，仅 GitHub 更新源会填充。</param>
/// <param name="AssetDownloadUrl">更新包下载地址，仅 GitHub 更新源会填充。</param>
/// <param name="AssetSize">更新包大小（字节），仅 GitHub 更新源会填充。</param>
internal sealed record PendingUpdateDescriptor(
    string SourceId,
    string Version,
    string SubChannel,
    string PackagingType,
    string? AssetName = null,
    string? AssetDownloadUrl = null,
    long AssetSize = 0);