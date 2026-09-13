using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClassIsland.Helpers;
using PhainonDistributionCenter.Shared.Models.Api.Responses.Distribution;
using PhainonDistributionCenter.Shared.Models.Client;

namespace ClassIsland.Services.AppUpdating.Sources;

/// <summary>
/// 从 ClassIsland 分发中心（PDCC）获取应用更新的更新源。该更新源为兼容保留，默认不启用。
/// </summary>
internal sealed class PhainonUpdateSource(WebRequestHelper requestHelper) : IUpdateSource
{
    /// <inheritdoc />
    public UpdateSourceKind Kind => UpdateSourceKind.PhainonDistributionCenter;

    /// <inheritdoc />
    public string DisplayName => "ClassIsland 分发中心";

    /// <inheritdoc />
    public async Task<UpdateSourceChannels> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        var metadata = await requestHelper.GetJson<DistributionMetadata>(
            new Uri("api/v1/public/distributions/metadata", UriKind.Relative),
            cancellationToken: cancellationToken);

        return new UpdateSourceChannels(
            metadata.Channels
                .Select(x => new UpdateSourceChannel(x.Key.ToString(), x.Value.Name, x.Value.Description))
                .ToArray(),
            metadata.DefaultChannelId.ToString());
    }

    /// <inheritdoc />
    public async Task<UpdateRelease?> GetLatestReleaseAsync(
        UpdateSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.ChannelId, out var channelId))
        {
            throw new InvalidOperationException("分发中心更新通道 ID 无效，请在设置中重新选择更新通道。");
        }

        var latest = await requestHelper.GetJson<LatestDistributionInfoMinResponse>(
            new Uri(
                $"api/v1/public/distributions/latest/{channelId}?appVersion={Uri.EscapeDataString(request.CurrentVersion)}",
                UriKind.Relative),
            cancellationToken: cancellationToken);
        var detail = await requestHelper.GetJson<DistributionInfoClient>(
            new Uri(
                $"api/v1/public/distributions/{latest.DistributionId}/{Uri.EscapeDataString(request.SubChannel)}",
                UriKind.Relative),
            cancellationToken: cancellationToken);

        return new UpdateRelease(
            detail.Version,
            string.IsNullOrWhiteSpace(detail.FriendlyVersion) ? detail.Version : detail.FriendlyVersion,
            detail.ChangeLog,
            null,
            [],
            detail.FileMapJson,
            detail.FileMapSignature,
            detail);
    }
}