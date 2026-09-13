using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Helpers;
using ClassIsland.Core.Helpers.Native;
using ClassIsland.Core.Models;
using ClassIsland.Core.Models.Updating;
using ClassIsland.Enums.AppUpdating;
using ClassIsland.Helpers;
using ClassIsland.Models;
using ClassIsland.Models.AppUpdating;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Services.AppUpdating.Sources;
using ClassIsland.Shared;
using ClassIsland.Shared.Enums;
using ClassIsland.Shared.Helpers;
using ClassIsland.Views;
using Downloader;
using DynamicData;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhainonDistributionCenter.Shared.Helpers;
using PhainonDistributionCenter.Shared.Models.Client;
using PhainonDistributionCenter.Shared.Models.FileMap;
using Sentry;
using Tmds.DBus.Protocol;
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;
using File = System.IO.File;

namespace ClassIsland.Services.AppUpdating;

public class UpdateService : IHostedService, INotifyPropertyChanged
{
    private UpdateWorkingStatus _currentWorkingStatus = UpdateWorkingStatus.Idle;
    public IDownload? Downloader;
    private bool _isCanceled = false;
    private Exception? _networkErrorException;
    private TimeSpan _downloadEtcSeconds = TimeSpan.Zero;
    private DistributionInfoClient _distributionInfo;
    private string _currentWorkingMessage = "";

    private const string PhainonRootUrl = "https://distribution.classisland.tech";

    private const string PendingUpdateFileName = "PendingUpdate.json";
    private const string GitHubPackageDirectoryName = "Package";

    private GitHubUpdateSource? _gitHubUpdateSource;
    private string? _gitHubUpdateSourceRepository;

    internal static string UpdateCachePath { get; } = Path.Combine(CommonDirectories.AppCacheFolderPath, "Update");

    public static string UpdateTempPath => Path.Combine(CommonDirectories.AppTempFolderPath, "Updating");

    private static string UpdateDistributionInfoPath { get; } = Path.Combine(UpdateCachePath, "DistributionInfo.json");
    private static string UpdateDistributionMetadataPath { get; } = Path.Combine(UpdateCachePath, "DistributionMetadata.json");

    private static string PendingUpdatePath => Path.Combine(UpdateCachePath, PendingUpdateFileName);
    
    public static readonly string[] AllowedPackageTypes = ["folder", "folderClassic", "installer"];

    private CancellationTokenSource? _downloadCancellationTokenSource;
    private int _downloadedCount = 0;
    private int _downloadingCountTotal = 0;
    private bool _isDownloadingProgressIndeterminate = false;
    private Exception? _deployErrorException;

    public ObservableCollection<DownloadTaskInfo> DownloadTasks { get; } = [];

    public DistributionInfoClient DistributionInfo
    {
        get => _distributionInfo;
        set => SetField(ref _distributionInfo, value);
    }

    public DistributionMetadata DistributionMetadata
    {
        get;
        set;
    }

    public UpdateWorkingStatus CurrentWorkingStatus
    {
        get => _currentWorkingStatus;
        set => SetField(ref _currentWorkingStatus, value);
    }

    private SettingsService SettingsService
    {
        get;
    }

    public string CurrentWorkingMessage
    {
        get => _currentWorkingMessage;
        set => SetField(ref _currentWorkingMessage, value);
    }

    public bool IsDownloadingProgressIndeterminate
    {
        get => _isDownloadingProgressIndeterminate;
        set => SetField(ref _isDownloadingProgressIndeterminate, value);
    }

    private Settings Settings => SettingsService.Settings;

    private ITaskBarIconService TaskBarIconService
    {
        get;
    }

    private ISplashService SplashService { get; }

    private ILogger<UpdateService> Logger { get; }

    private string MetadataPublisherPublicKey { get; }

    public event EventHandler? UpdateInfoUpdated;
    
    private WebRequestHelper RequestHelper { get; }

    public int DownloadingCountTotal
    {
        get => _downloadingCountTotal;
        set
        {
            if (value == _downloadingCountTotal) return;
            _downloadingCountTotal = value;
            OnPropertyChanged();
        }
    }

    public int DownloadedCount
    {
        get => _downloadedCount;
        set
        {
            if (value == _downloadedCount) return;
            _downloadedCount = value;
            OnPropertyChanged();
        }
    }

    public UpdateService(SettingsService settingsService, ITaskBarIconService taskBarIconService, IHostApplicationLifetime lifetime,
        ISplashService splashService, ILogger<UpdateService> logger)
    {
        SettingsService = settingsService;
        TaskBarIconService = taskBarIconService;
        SplashService = splashService;
        Logger = logger;

        var keyStream = AssetLoader.Open(new Uri("avares://ClassFabric/Assets/TrustedPublicKeys/ClassFabric.MetadataPublisher.asc", UriKind.RelativeOrAbsolute));
        MetadataPublisherPublicKey = new StreamReader(keyStream).ReadToEnd();

        RequestHelper = new WebRequestHelper(AppBase.Current.IsDevelopmentBuild 
                                             && !string.IsNullOrWhiteSpace(Settings.DebugPhainonRootUrlOverride) 
                                             && Uri.TryCreate(Settings.DebugPhainonRootUrlOverride, UriKind.Absolute, out var u1)
            ? u1
            : new Uri(PhainonRootUrl), true);
        _distributionInfo = ConfigureFileHelper.LoadConfig<DistributionInfoClient>(UpdateDistributionInfoPath);
        DistributionMetadata = ConfigureFileHelper.LoadConfig<DistributionMetadata>(UpdateDistributionMetadataPath);
    }

    public bool IsCanceled
    {
        get => _isCanceled;
        set => SetField(ref _isCanceled, value);
    }

    public Exception? NetworkErrorException
    {
        get => _networkErrorException;
        set => SetField(ref _networkErrorException, value);
    }

    public Exception? DeployErrorException
    {
        get => _deployErrorException;
        set => SetField(ref _deployErrorException, value);
    }

    public async Task<bool> AppStartup()
    {
        _ = AppStartupBackground();
        return false;
    }

    private async Task AppStartupBackground()
    {
        if (Settings.LastUpdateStatus == UpdateStatus.UpdateDeployed)
        {
            CleanupPrevDeployments();
            await PlatformServices.DesktopToastService.ShowToastAsync("更新成功",
                $"应用已更新到 {AppBase.AppVersion}，点击以查看详细信息。", UpdateNotificationClickedCallback);
            Settings.LastUpdateStatus = UpdateStatus.UpToDate;
        }
        
        if (Settings.UpdateMode < 1)
        {
            return;
        }
        
        await CheckUpdateAsync();

        if (Settings.UpdateMode < 2)
        {
            return;
        }

        if (Settings.LastUpdateStatus == UpdateStatus.UpdateAvailable)
        {
            await DownloadUpdateAsync();
        }

        if (Settings.UpdateMode < 3)
        {
            return;
        }

        if (Settings.LastUpdateStatus == UpdateStatus.UpdateDownloaded)
        {
            await ExtractUpdateAsync();
        }
    }

    public static void RemoveUpdateTemporary(string target)
    {
        if (File.Exists(target))
        {
            NativeWindowHelper.WaitForFile(target);
            File.Delete(target);
        }
        try
        {
            Directory.Delete(UpdateTempPath, true);
        }
        catch (Exception e)
        {
            // ignored
            Console.WriteLine(e);
        }
    }


    public async Task CheckUpdateAsync(bool isForce=false, bool isCancel=false)
    {
        if (!AllowedPackageTypes.Contains(AppBase.Current.PackagingType))
        {
            return;
        }
        var transaction = SentrySdk.StartTransaction("Get Update Info", "appUpdating.getMetadata");
        if (!isCancel)
        {
            NetworkErrorException = null;
            DeployErrorException = null;
        }
        try
        {
            var spanGetIndex = transaction.StartChild("getIndex");
            var subChannel = GetCurrentSubChannel();
            CurrentWorkingStatus = UpdateWorkingStatus.CheckingUpdates;
            var latest = await FindLatestReleaseAsync(subChannel, isForce);
            PersistUpdateCache(UpdateDistributionMetadataPath, DistributionMetadata);
            var release = latest.Release;
            spanGetIndex.Finish(SpanStatus.Ok);
            if (release == null || !IsNewerVersion(isForce, isCancel, ParseVersionOrThrow(release.Version)))
            {
                Settings.LastUpdateStatus = UpdateStatus.UpToDate;
                transaction.Finish(SpanStatus.Ok);
                return;
            }

            var spanGetDetail = transaction.StartChild("getDetail");
            DistributionInfo = BuildDistributionInfo(release, subChannel);
            PersistUpdateCache(UpdateDistributionInfoPath, DistributionInfo);
            await SavePendingUpdateAsync(latest.Source, release, subChannel);
            Settings.LastUpdateStatus = UpdateStatus.UpdateAvailable;
            await PlatformServices.DesktopToastService.ShowToastAsync("发现新版本",
                $"{AppBase.AppVersion} -> {release.FriendlyVersion}" + Environment.NewLine +
                "点击以查看详细信息。", UpdateNotificationClickedCallback);
            spanGetDetail.Finish(SpanStatus.Ok);
            transaction.Finish(SpanStatus.Ok);
        }
        catch (Exception ex)
        {
            Settings.LastUpdateStatus = UpdateStatus.UpToDate;
            NetworkErrorException = ex;
            transaction.GetLastActiveSpan()?.Finish(ex, SpanStatus.InternalError);
            transaction.Finish(ex, SpanStatus.InternalError);
            Logger.LogError(ex, "检查应用更新失败。");
        }
        finally
        {
            Settings.LastCheckUpdateTime = DateTime.Now;
            UpdateInfoUpdated?.Invoke(this, EventArgs.Empty);
            CurrentWorkingStatus = UpdateWorkingStatus.Idle;
        }
    }

    private string GetCurrentSubChannel()
    {
        return AppBase.Current.IsDevelopmentBuild && !string.IsNullOrWhiteSpace(Settings.DebugSubChannelOverride)
            ? Settings.DebugSubChannelOverride
            : AppBase.Current.AppSubChannel;
    }

    private async Task<(IUpdateSource Source, UpdateRelease? Release)> FindLatestReleaseAsync(
        string subChannel, bool isForce)
    {
        var sourceId = UpdateSourceIds.Normalize(Settings.UpdateSourceId);
        if (Settings.UpdateSourceId != sourceId)
        {
            Settings.UpdateSourceId = sourceId;
        }

        var chain = new List<IUpdateSource>();
        switch (sourceId)
        {
            case UpdateSourceIds.PhainonDistributionCenter:
                chain.Add(CreatePhainonUpdateSource());
                break;
            case UpdateSourceIds.GitHubWithPhainonFallback:
                chain.Add(GetGitHubUpdateSource());
                chain.Add(CreatePhainonUpdateSource());
                break;
            default:
                chain.Add(GetGitHubUpdateSource());
                break;
        }

        Exception? primaryError = null;
        foreach (var source in chain)
        {
            try
            {
                var channels = await source.GetChannelsAsync();
                ApplyChannels(source, channels);
                var request = new UpdateSourceRequest(
                    ResolveChannelId(source),
                    subChannel,
                    AppBase.Current.PackagingType,
                    AppBase.AppVersion,
                    isForce);
                var release = await source.GetLatestReleaseAsync(request);
                Logger.LogInformation("已使用更新源 {Source} 完成更新检查。", source.DisplayName);
                return (source, release);
            }
            catch (Exception ex)
            {
                primaryError ??= ex;
                Logger.LogWarning(ex, "更新源 {Source} 检查更新失败。", source.DisplayName);
            }
        }

        throw primaryError ?? new InvalidOperationException("没有可用的应用更新源，请检查更新源设置。");
    }

    private GitHubUpdateSource GetGitHubUpdateSource()
    {
        var repository = string.IsNullOrWhiteSpace(Settings.GitHubUpdateRepository)
            ? UpdateSourceIds.DefaultGitHubRepository
            : Settings.GitHubUpdateRepository.Trim();
        if (_gitHubUpdateSource == null || _gitHubUpdateSourceRepository != repository)
        {
            _gitHubUpdateSource?.Dispose();
            _gitHubUpdateSource = new GitHubUpdateSource(repository, UpdateCachePath, logger: Logger);
            _gitHubUpdateSourceRepository = repository;
        }

        return _gitHubUpdateSource;
    }

    private PhainonUpdateSource CreatePhainonUpdateSource() => new(RequestHelper);

    private void ApplyChannels(IUpdateSource source, UpdateSourceChannels channels)
    {
        var metadata = new DistributionMetadata();
        foreach (var channel in channels.Channels)
        {
            if (Guid.TryParse(channel.Id, out var channelGuid))
            {
                metadata.Channels[channelGuid] = new DistributionMetadata.DistributionChannel
                {
                    Name = channel.Name,
                    Description = channel.Description
                };
            }
        }

        metadata.DefaultChannelId = channels.DefaultChannelId != null &&
                                    Guid.TryParse(channels.DefaultChannelId, out var defaultChannelId) &&
                                    metadata.Channels.ContainsKey(defaultChannelId)
            ? defaultChannelId
            : metadata.Channels.Keys.FirstOrDefault();
        DistributionMetadata = metadata;

        var selected = source is GitHubUpdateSource
            ? GitHubUpdateSource.GetChannelGuid(Settings.GitHubUpdateChannel)
            : Settings.SelectedUpdateChannelV3;
        if (!metadata.Channels.ContainsKey(selected) && metadata.Channels.Count > 0)
        {
            selected = metadata.DefaultChannelId;
        }

        if (Settings.SelectedUpdateChannelV3 != selected)
        {
            Settings.SelectedUpdateChannelV3 = selected;
        }

        if (source is GitHubUpdateSource)
        {
            var channelId = GitHubUpdateSource.GetChannelIdFromGuid(selected);
            if (Settings.GitHubUpdateChannel != channelId)
            {
                Settings.GitHubUpdateChannel = channelId;
            }
        }
    }

    /// <summary>
    /// 仅根据本地信息刷新更新通道列表，不发起网络请求。供设置界面在切换更新源后立即刷新通道下拉框。
    /// </summary>
    internal void RefreshLocalChannelMetadata()
    {
        var sourceId = UpdateSourceIds.Normalize(Settings.UpdateSourceId);
        if (sourceId == UpdateSourceIds.GitHub || sourceId == UpdateSourceIds.GitHubWithPhainonFallback)
        {
            ApplyChannels(GetGitHubUpdateSource(), GitHubUpdateSource.CreateChannels());
        }
    }

    private string ResolveChannelId(IUpdateSource source) => source is GitHubUpdateSource
        ? GitHubUpdateSource.GetChannelIdFromGuid(Settings.SelectedUpdateChannelV3)
        : Settings.SelectedUpdateChannelV3.ToString();

    private static DistributionInfoClient BuildDistributionInfo(UpdateRelease release, string subChannel) =>
        new()
        {
            Version = release.Version,
            FriendlyVersion = release.FriendlyVersion,
            FriendlyVersionShort = release.FriendlyVersion,
            ChangeLog = release.ChangeLog,
            SubChannel = subChannel,
            FileMapJson = release.FileMapJson ?? "",
            FileMapSignature = release.FileMapSignature ?? ""
        };

    private void PersistUpdateCache<T>(string path, T value) where T : class
    {
        try
        {
            ConfigureFileHelper.SaveConfig(path, value);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "保存更新缓存 {} 失败。", path);
        }
    }

    private async Task SavePendingUpdateAsync(IUpdateSource source, UpdateRelease release, string subChannel)
    {
        var asset = release.Assets.FirstOrDefault();
        var descriptor = new PendingUpdateDescriptor(
            source.Kind == UpdateSourceKind.GitHub
                ? UpdateSourceIds.GitHub
                : UpdateSourceIds.PhainonDistributionCenter,
            release.Version,
            subChannel,
            AppBase.Current.PackagingType,
            asset?.Name,
            asset?.DownloadUri.ToString(),
            asset?.Size ?? 0);
        try
        {
            Directory.CreateDirectory(UpdateCachePath);
            await File.WriteAllTextAsync(PendingUpdatePath, JsonSerializer.Serialize(descriptor));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "保存待处理更新信息失败。");
        }
    }

    private PendingUpdateDescriptor? LoadPendingUpdate()
    {
        try
        {
            return File.Exists(PendingUpdatePath)
                ? JsonSerializer.Deserialize<PendingUpdateDescriptor>(File.ReadAllText(PendingUpdatePath))
                : null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "读取待处理更新信息失败。");
            return null;
        }
    }

    private void ClearPendingUpdate()
    {
        try
        {
            if (File.Exists(PendingUpdatePath))
            {
                File.Delete(PendingUpdatePath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "清理待处理更新信息失败。");
        }
    }

    private void UpdateNotificationClickedCallback()
    {
        IAppHost.GetService<IUriNavigationService>().NavigateWrapped(new Uri("classfabric://app/settings/update"));
    }

    private static Version CurrentAppVersion =>
        Version.TryParse(AppBase.AppVersion, out var version) ? version : new Version(0, 0, 0, 0);

    private static Version ParseVersionOrThrow(string version) =>
        Version.TryParse(version, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"更新源返回了无法识别的版本号：{version}");

    private bool IsNewerVersion(bool isForce, bool isCancel, Version verCode)
    {
        return (verCode > CurrentAppVersion &&
                (Settings.LastUpdateStatus != UpdateStatus.UpdateDownloaded || isCancel)) // 正常更新
               || isForce;
    }

    public async Task DownloadUpdateAsync()
    {
        IsDownloadingProgressIndeterminate = true;
        if (!AllowedPackageTypes.Contains(AppBase.Current.PackagingType))
        {
            return;
        }
        if (Design.IsDesignMode)
        {
            return;
        }
        var transaction = SentrySdk.StartTransaction("Download Update", "appUpdating.download");
        var spanDeletePreviousFile = transaction.StartChild("deletePreviousFile");
        try
        {
            if (Directory.Exists(UpdateTempPath))
            {
                Directory.Delete(UpdateTempPath, true);
            }
            spanDeletePreviousFile.Finish(SpanStatus.Ok);
        }
        catch (Exception ex)
        {
            spanDeletePreviousFile.Finish(ex, SpanStatus.InternalError);
            Logger.LogError(ex, "移除下载临时文件失败。");
        }

        var spanDownload = transaction.StartChild("download");
        if (_downloadCancellationTokenSource != null)
        {
            await _downloadCancellationTokenSource.CancelAsync();
        }

        try
        {
            var pendingUpdate = LoadPendingUpdate();
            if (pendingUpdate?.SourceId == UpdateSourceIds.GitHub)
            {
                await DownloadGitHubUpdateAsync(pendingUpdate);
                return;
            }

            var publicKey =
                AppBase.Current.IsDevelopmentBuild && !string.IsNullOrWhiteSpace(Settings.DebugPublicKeyOverride)
                    ? Settings.DebugPublicKeyOverride
                    : MetadataPublisherPublicKey;
            var valid = DetachedSignatureProcessor.VerifyDetachedSignature(DistributionInfo.FileMapJson,
                Encoding.UTF8.GetBytes(DistributionInfo.FileMapSignature), publicKey);
            if (!valid)
            {
                throw new InvalidOperationException("文件图签名校验不通过");
            }

            var fileMap = JsonSerializer.Deserialize<FileMap>(DistributionInfo.FileMapJson);
            if (fileMap == null)
            {
                throw new InvalidOperationException("文件图解析失败");
            }

            CurrentWorkingStatus = UpdateWorkingStatus.DownloadingUpdates;
            var cts = _downloadCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cts.Token;

            var options = new DownloadConfiguration()
            {
                ChunkCount = 4,
                ParallelCount = 4,
                ParallelDownload = true,
                BlockTimeout = 60_000,
                HttpClientTimeout = 60_000
            };
            // key 是 hash（HEX）
            Dictionary<string, (string Name, FileMapFile FileInfo)> filesHashed = [];
            var deploymentLock = new DeploymentLock()
            {
                SubChannel = GetCurrentSubChannel(),
                FileMapSha512 = SHA512.HashData(Encoding.UTF8.GetBytes(DistributionInfo.FileMapJson))
            };

            Logger.LogTrace("正在计算要下载的文件");
            var prevFileMapPath = Path.Combine(Environment.CurrentDirectory, "files.json");
            var prevFileMapUserVarsPath = Path.Combine(Environment.CurrentDirectory, "files.uvars.json");
            var prevFileMap = new FileMap();
            var prevUserVars = new Dictionary<string, string>();
            var prevFileMapUserVarsLoadSuccess = false;
            if (File.Exists(prevFileMapPath))
            {
                try
                {
                    prevFileMap = ConfigureFileHelper.LoadConfigUnWrapped<FileMap>(prevFileMapPath, false);
                }
                catch (Exception e)
                {
                    Logger.LogWarning(e, "无法加载当前版本的文件图 {}", prevFileMapPath);
                }
            }
            if (File.Exists(prevFileMapUserVarsPath))
            {
                try
                {
                    prevUserVars = ConfigureFileHelper.LoadConfigUnWrapped<Dictionary<string, string>>(prevFileMapUserVarsPath, false);
                    prevFileMapUserVarsLoadSuccess = true;
                }
                catch (Exception e)
                {
                    Logger.LogWarning(e, "无法加载当前版本的部署用户变量 {}", prevFileMapUserVarsPath);
                }
            }
            var vars = ConfigureFileHelper.CopyObject(prevFileMap.Variables);
            if (prevFileMapUserVarsLoadSuccess)
            {
                foreach (var (k, v) in prevUserVars)
                {
                    vars[k] = v;
                }
            }

            var root = CommonDirectories.AppPackageRoot;
            foreach (var (id, component) in fileMap.Components)
            {
                var prevComp = prevFileMap.Components.GetValueOrDefault(id);
                var existedFiles = new List<string>();
                deploymentLock.ExistedFiles[id] = existedFiles;

                string? prevCompRoot = null;
                if (prevFileMapUserVarsLoadSuccess)
                {
                    prevCompRoot = Path.GetFullPath(Path.Combine(root,
                        VariableStringHelpers.ExpandString(component.Root, vars)));
                    if (!PathHelpers.IsSafePath(root, prevCompRoot))
                    {
                        throw new InvalidOperationException("文件图组件根目录无效");
                    }
                    deploymentLock.ComponentRoots[id] = prevCompRoot;
                }

                foreach (var (path, fileInfo) in component.Files)
                {
                    if (prevFileMapUserVarsLoadSuccess && prevCompRoot != null &&
                        Path.Exists(Path.Combine(prevCompRoot, path)) &&
                        component.AllowDiffUpdate &&
                        prevComp?.Files.GetValueOrDefault(path)?.FileSha512.SequenceEqual(fileInfo.FileSha512) == true)
                    {
                        await using var file = File.OpenRead(Path.Combine(prevCompRoot, path));
                        var hash = await SHA512.HashDataAsync(file, cancellationToken);
                        if (hash.SequenceEqual(fileInfo.FileSha512))
                        {
                            existedFiles.Add(path);
                            Logger.LogTrace("SKIP {}/{}", id, path);
                            continue;
                        }
                    }

                    Logger.LogTrace("ADD {}/{}", id, path);
                    filesHashed.TryAdd(Convert.ToHexString(fileInfo.FileSha512), (Path.GetFileName(path), fileInfo));
                }
            }

            var dlRoot = UpdateTempPath;
            if (!Directory.Exists(dlRoot))
            {
                Directory.CreateDirectory(dlRoot);
            }

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 8,
                CancellationToken = cancellationToken
            };
            DownloadedCount = 0;
            DownloadingCountTotal = filesHashed.Count;
            IsDownloadingProgressIndeterminate = false;
            Logger.LogInformation("开始下载更新，要下载 {} 个文件", filesHashed.Count);
            var downloadTasksMap = new Dictionary<string, DownloadTaskInfo>();
            DownloadTasks.Clear();
            DownloadTasks.AddRange(filesHashed.Select(x => new DownloadTaskInfo()
            {
                FileName = x.Value.Name,
                Key = x.Key
            }));
            foreach (var info in DownloadTasks)
            {
                downloadTasksMap[info.Key] = info;
            }

            await Parallel.ForEachAsync(filesHashed, parallelOptions, async (pair, token) =>
            {
                var (hashHex, (fileName, file)) = pair;
                var updateStopwatch = Stopwatch.StartNew();
                Logger.LogInformation("开始下载 {}({})", fileName, file.ArchiveDownloadUrl);
                var info = downloadTasksMap.GetValueOrDefault(hashHex) ?? DownloadTaskInfo.CreateEmpty();
                info.State = DownloadState.Downloading;
                await using var downloader = DownloadBuilder.New()
                    .WithConfiguration(options)
                    .WithUrl(file.ArchiveDownloadUrl)
                    .WithFileLocation(Path.Combine(dlRoot, hashHex[..2], hashHex))
                    .Build();
                var taskCompletionSource = new TaskCompletionSource();
                downloader.DownloadFileCompleted += (_, args) =>
                {
                    if (args.Error != null)
                    {
                        taskCompletionSource.SetException(args.Error);
                        return;
                    }

                    Dispatcher.UIThread.InvokeAsync(() => { DownloadedCount++; });
                    taskCompletionSource.SetResult();
                };
                downloader.DownloadProgressChanged += (_, e) =>
                {
                    if (updateStopwatch.ElapsedMilliseconds < 250)
                        return;
                    updateStopwatch.Restart();
                    var totalSize = e.TotalBytesToReceive;
                    var downloadedSize = e.ReceivedBytesSize;
                    var downloadSpeed = e.BytesPerSecondSpeed;

                    var eta = TimeSpanHelper.FromSecondsSafe(downloadSpeed == 0
                        ? 0
                        : (long)((totalSize - downloadedSize) / downloadSpeed));
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        info.FileSize = totalSize;
                        info.DownloadedSize = downloadedSize;
                        info.DownloadSpeed = downloadSpeed;
                        info.TimeToComplete = eta;
                    });
                };
                token.Register(() => { taskCompletionSource.SetCanceled(token); });
                await downloader.StartAsync(token);
                await taskCompletionSource.Task;
                info.State = DownloadState.Completed;
                // DownloadTasks.Remove(info);
                Logger.LogInformation("下载完成 {}({})", fileName, file.ArchiveDownloadUrl);
            });

            await File.WriteAllTextAsync(Path.Combine(UpdateTempPath, "FileMap.json"), DistributionInfo.FileMapJson,
                cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(UpdateTempPath, "FileMap.json.sig"),
                DistributionInfo.FileMapSignature, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(UpdateTempPath, "Deployment.lock"),
                JsonSerializer.Serialize(deploymentLock), cancellationToken);
            Logger.LogInformation("全部下载完成！");
            Settings.LastUpdateStatus = UpdateStatus.UpdateDownloaded;
        }
        catch (TaskCanceledException)
        {
            transaction.Finish(SpanStatus.Cancelled);
            Logger.LogInformation("已取消下载更新");
        }
        catch (Exception ex)
        {
            NetworkErrorException = ex;
            transaction.Finish(ex, SpanStatus.InternalError);
            Logger.LogError(ex, "下载应用更新失败");
            await RemoveDownloadedFiles(true);
        }
        finally
        {
            if (CurrentWorkingStatus == UpdateWorkingStatus.DownloadingUpdates)
            {
                CurrentWorkingStatus = UpdateWorkingStatus.Idle;
            }
            _downloadCancellationTokenSource = null;
        }
    }

    public async Task StopDownloading()
    {
        Logger.LogInformation("应用更新下载停止。");
        IsCanceled = true;
        if (_downloadCancellationTokenSource != null)
        {
            try
            {
                await _downloadCancellationTokenSource.CancelAsync();
            }
            catch (Exception e)
            {
                // ignored
            }
        }

        _downloadCancellationTokenSource = null;
        Settings.LastUpdateStatus = UpdateStatus.UpToDate;
        CurrentWorkingStatus = UpdateWorkingStatus.Idle;
        await RemoveDownloadedFiles(true);
    }

    public async Task RemoveDownloadedFiles(bool isCancel)
    {
        try
        {
            Directory.Delete(UpdateTempPath, true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "移除下载临时文件失败。");
        }

        if (!isCancel)
        {
            return;
        }
        await CheckUpdateAsync(isCancel:true);
    }
    

    public async Task ExtractUpdateAsync()
    {
        DeployErrorException = null;
        if (!AllowedPackageTypes.Contains(AppBase.Current.PackagingType))
        {
            return;
        }
        CurrentWorkingStatus = UpdateWorkingStatus.ExtractingUpdates;
        try
        {
            Logger.LogInformation("正在部署应用更新");
            var pendingUpdate = LoadPendingUpdate();
            if (pendingUpdate?.SourceId == UpdateSourceIds.GitHub)
            {
                await DeployGitHubUpdateAsync(pendingUpdate);
                return;
            }

            var fileMapJson = await File.ReadAllTextAsync(Path.Combine(UpdateTempPath, "FileMap.json"));
            var fileMapSig = await File.ReadAllTextAsync(Path.Combine(UpdateTempPath, "FileMap.json.sig"));
            var deploymentLock = ConfigureFileHelper.LoadConfigUnWrapped<DeploymentLock>(Path.Combine(UpdateTempPath, "Deployment.lock"), false);
            if (!deploymentLock.FileMapSha512.SequenceEqual(SHA512.HashData(Encoding.UTF8.GetBytes(fileMapJson))))
            {
                throw new InvalidOperationException("文件图哈希与下载时不符合，可能已经损坏");
            }
            if (deploymentLock.SubChannel != GetCurrentSubChannel())
            {
                throw new InvalidOperationException("下载的更新不适用于当前子频道的 ClassFabric");
            }
            var publicKey =
                AppBase.Current.IsDevelopmentBuild && !string.IsNullOrWhiteSpace(Settings.DebugPublicKeyOverride)
                    ? Settings.DebugPublicKeyOverride
                    : MetadataPublisherPublicKey;
            var valid = DetachedSignatureProcessor.VerifyDetachedSignature(fileMapJson,
                Encoding.UTF8.GetBytes(fileMapSig), publicKey);
            if (!valid)
            {
                throw new InvalidOperationException("文件图签名校验不通过");
            }

            var fileMap = JsonSerializer.Deserialize<FileMap>(fileMapJson);
            if (fileMap == null)
            {
                throw new InvalidOperationException("文件图解析失败");
            }

            Logger.LogInformation("正在解压并检验文件完整性");
            
            var root = CommonDirectories.AppPackageRoot;
            
            Logger.LogTrace("DeployRoot = {}", root);
            var extractedPath = Path.Combine(UpdateTempPath, "extracted");
            if (!Directory.Exists(extractedPath))
            {
                Directory.CreateDirectory(extractedPath);
            }
            
            foreach (var (id, component) in fileMap.Components)
            {
                var existedFiles = deploymentLock.ExistedFiles.GetValueOrDefault(id, []);

                foreach (var (path, fileInfo) in component.Files.Where(x =>
                             !component.AllowDiffUpdate || !existedFiles.Contains(x.Key)))
                {
                    var hashHex = Convert.ToHexString(fileInfo.FileSha512);
                    var fullPath = Path.Combine(extractedPath, hashHex[..2], hashHex);
                    if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Path is null"));
                    }

                    Logger.LogTrace("正在解压：{}({})", path, fullPath);
                    await using (var file = File.OpenWrite(fullPath))
                    {
                        await using var archive = File.OpenRead(Path.Combine(UpdateTempPath, hashHex[..2], hashHex));
                        archive.Position = 0;
                        await using var gZipStream = new GZipStream(archive, CompressionMode.Decompress);
                        await gZipStream.CopyToAsync(file);
                    }

                    await using (var file = File.OpenRead(fullPath))
                    {
                        var hash = await SHA512.HashDataAsync(file);
                        if (!hash.SequenceEqual(fileInfo.FileSha512))
                        {
                            throw new InvalidOperationException($"文件 {id}/{path} 的 SHA512 校验失败 ");
                        }
                    }
                }
            }
            
            Logger.LogTrace("正在检查文件目录");

            var num = 0;
            fileMap.Variables["number"] = num.ToString();
            while (num <= 255 && Directory.Exists(Path.Combine(root,
                       VariableStringHelpers.ExpandString(fileMap.Components["app"].Root, fileMap.Variables))))
            {
                num++;
                fileMap.Variables["number"] = num.ToString();
            }
            
            foreach (var (id, component) in fileMap.Components)
            {
                var compRoot = Path.Combine(root,
                    VariableStringHelpers.ExpandString(component.Root, fileMap.Variables));

                if (!PathHelpers.IsSafePath(root, compRoot) || 
                    component.Files.Any(x => 
                        !PathHelpers.IsSafePath(compRoot, x.Key)))
                {
                    throw new InvalidOperationException("文件图发现非法文件路径");
                }

                var existedFiles = deploymentLock.ExistedFiles.GetValueOrDefault(id, []);
                var existedFilesRoot = deploymentLock.ComponentRoots.GetValueOrDefault(id);

                foreach (var (path, fileInfo) in component.Files)
                {
                    var targetPath = Path.Combine(compRoot, path);

                    if (component.AllowDiffUpdate && existedFiles.Contains(path) && existedFilesRoot != null)
                    {
                        var existedPath = Path.Combine(existedFilesRoot, path);
                        if (Path.Exists(existedPath))
                        {
                            await using var file = File.OpenRead(existedPath);
                            var hash = await SHA512.HashDataAsync(file);
                            if (hash.SequenceEqual(fileInfo.FileSha512))
                            {
                                Logger.LogTrace("Deploy Check EXISTED {} -> {}", existedPath, targetPath);
                                continue;
                            }
                        }
                    }
                    
                    var hashHex = Convert.ToHexString(fileInfo.FileSha512);
                    var fullPath = Path.Combine(extractedPath, hashHex[..2], hashHex);
                    if (File.Exists(fullPath))
                    {
                        Logger.LogTrace("Deploy EXISTED {} -> {}", fullPath, targetPath);
                        continue;
                    }

                    throw new InvalidOperationException($"文件 {id}/{path} 未找到部署源");
                }
            }
            Logger.LogInformation("Variables: {}", JsonSerializer.Serialize(fileMap.Variables));
            
            Logger.LogInformation("正在准备部署");

            var appPath = Path.Combine(root,
                VariableStringHelpers.ExpandString(fileMap.Components["app"].Root, fileMap.Variables));
            if (!Directory.Exists(appPath))
            {
                Directory.CreateDirectory(appPath);
                await File.WriteAllTextAsync(Path.Combine(appPath, ".partial"), "");
            }
            
            
            foreach (var (id, component) in fileMap.Components)
            {
                var existedFiles = deploymentLock.ExistedFiles.GetValueOrDefault(id, []);
                var existedFilesRoot = deploymentLock.ComponentRoots.GetValueOrDefault(id);
                var compRoot = Path.Combine(root,
                    VariableStringHelpers.ExpandString(component.Root, fileMap.Variables));

                await Parallel.ForEachAsync(component.Files, new ParallelOptions()
                {
                    MaxDegreeOfParallelism = 16
                }, async (pair, token) =>
                {
                    var (path, fileInfo) = pair;
                    var targetPath = Path.Combine(compRoot, path);
                    var dir = Path.GetDirectoryName(targetPath);
                    if (dir != null && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (component.AllowDiffUpdate && existedFiles.Contains(path) && existedFilesRoot != null)
                    {
                        var existedPath = Path.Combine(existedFilesRoot, path);

                        Logger.LogTrace("Deploy Copy EXISTED {} -> {}", existedPath, targetPath);
                        File.Copy(existedPath, targetPath, true);
                        return;
                    }

                    var hashHex = Convert.ToHexString(fileInfo.FileSha512);
                    var fullPath = Path.Combine(extractedPath, hashHex[..2], hashHex);
                    Logger.LogTrace("Deploy Copy {} -> {}", fullPath, targetPath);
                    File.Copy(fullPath, targetPath, true);
                });
            }

            if (OperatingSystem.IsLinux() && AppBase.Current.PackagingType == "folder")
            {
                using var proc = Process.Start(new ProcessStartInfo("chmod",
                [
                    "+x",
                    Path.GetFullPath(Path.Combine(root,
                        "ClassFabric"))
                ]));
                var task = proc?.WaitForExitAsync();
                if (task != null)
                {
                    await task;
                }
            }
            File.Copy(Path.Combine(UpdateTempPath, "FileMap.json"), Path.Combine(appPath, "files.json"));
            ConfigureFileHelper.SaveConfig(Path.Combine(appPath, "files.uvars.json"), fileMap.Variables);
            
            Logger.LogInformation("正在激活新的部署");
            
            await File.WriteAllTextAsync(Path.Combine(appPath, ".current"), "");
            await File.WriteAllTextAsync(Path.Combine(Environment.CurrentDirectory, ".destroy"), "");
            File.Delete(Path.Combine(appPath, ".partial"));
            foreach (var deployment in Directory.GetDirectories(root)
                         .Where(x => Path.GetFileName(x).StartsWith("app") 
                                     && Path.GetFullPath(Path.Combine(root, x)) != Path.GetFullPath(Environment.CurrentDirectory)
                                     && File.Exists(Path.Combine(x, ".current"))))
            {
                File.Delete(Path.Combine(deployment, ".current"));
            }

            Settings.LastUpdateStatus = UpdateStatus.UpdateDeployed;
            Logger.LogInformation("部署成功");
            await RemoveDownloadedFiles(false);
        }
        catch (Exception e)
        {
            DeployErrorException = e;
            Logger.LogError(e, "无法部署应用更新");
            await RemoveDownloadedFiles(true);
        }
        finally
        {
            CurrentWorkingStatus = UpdateWorkingStatus.Idle;
        }

        return;

        string GetExistedFileRoot(string id) => id switch
        {
            "app" => Environment.CurrentDirectory,
            _ => throw new ArgumentOutOfRangeException()
        }; 
    }

    public void CleanupPrevDeployments()
    {
        var root = CommonDirectories.AppPackageRoot;
        foreach (var deployment in Directory.GetDirectories(root)
                     .Where(x => Path.GetFileName(x).StartsWith("app") 
                                 && Path.GetFullPath(Path.Combine(root, x)) != Path.GetFullPath(Environment.CurrentDirectory)
                                 && File.Exists(Path.Combine(x, ".destroy"))))
        {
            Logger.LogInformation("正在清理先前的部署：{}", deployment);
            try
            {
                Directory.Delete(Path.Combine(root, deployment), true);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "无法清理部署 {}", deployment);
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        return;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        return;
    }


    private async Task DownloadGitHubUpdateAsync(PendingUpdateDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.AssetName) ||
            !Uri.TryCreate(descriptor.AssetDownloadUrl, UriKind.Absolute, out var downloadUri))
        {
            throw new InvalidOperationException("待处理更新缺少更新包下载信息，请重新检查更新。");
        }

        var dlRoot = Path.Combine(UpdateTempPath, GitHubPackageDirectoryName);
        Directory.CreateDirectory(dlRoot);
        CurrentWorkingStatus = UpdateWorkingStatus.DownloadingUpdates;
        var cts = _downloadCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        IsDownloadingProgressIndeterminate = false;
        DownloadedCount = 0;
        DownloadingCountTotal = 1;
        DownloadTasks.Clear();
        var info = new DownloadTaskInfo
        {
            FileName = descriptor.AssetName,
            Key = descriptor.AssetName
        };
        DownloadTasks.Add(info);

        var options = new DownloadConfiguration()
        {
            ChunkCount = 4,
            ParallelCount = 4,
            ParallelDownload = true,
            BlockTimeout = 60_000,
            HttpClientTimeout = 60_000
        };
        var targetPath = Path.GetFullPath(Path.Combine(dlRoot, descriptor.AssetName));
        var updateStopwatch = Stopwatch.StartNew();
        await using var downloader = DownloadBuilder.New()
            .WithConfiguration(options)
            .WithUrl(downloadUri.ToString())
            .WithFileLocation(targetPath)
            .Build();
        var taskCompletionSource = new TaskCompletionSource();
        downloader.DownloadFileCompleted += (_, args) =>
        {
            if (args.Error != null)
            {
                taskCompletionSource.SetException(args.Error);
                return;
            }

            Dispatcher.UIThread.InvokeAsync(() => { DownloadedCount++; });
            taskCompletionSource.SetResult();
        };
        downloader.DownloadProgressChanged += (_, e) =>
        {
            if (updateStopwatch.ElapsedMilliseconds < 250)
                return;
            updateStopwatch.Restart();
            var totalSize = e.TotalBytesToReceive;
            var downloadedSize = e.ReceivedBytesSize;
            var downloadSpeed = e.BytesPerSecondSpeed;
            var eta = TimeSpanHelper.FromSecondsSafe(downloadSpeed == 0
                ? 0
                : (long)((totalSize - downloadedSize) / downloadSpeed));
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                info.FileSize = totalSize;
                info.DownloadedSize = downloadedSize;
                info.DownloadSpeed = downloadSpeed;
                info.TimeToComplete = eta;
            });
        };
        info.State = DownloadState.Downloading;
        cancellationToken.Register(() => { taskCompletionSource.SetCanceled(cancellationToken); });
        await downloader.StartAsync(cancellationToken);
        await taskCompletionSource.Task;
        info.State = DownloadState.Completed;
        Logger.LogInformation("更新包下载完成：{}", targetPath);
        Settings.LastUpdateStatus = UpdateStatus.UpdateDownloaded;
    }

    private async Task DeployGitHubUpdateAsync(PendingUpdateDescriptor descriptor)
    {
        var packagePath = Path.Combine(UpdateTempPath, GitHubPackageDirectoryName, descriptor.AssetName ?? "");
        if (!File.Exists(packagePath))
        {
            throw new InvalidOperationException("未找到已下载的更新包，请重新下载更新。");
        }

        if (descriptor.PackagingType == "installer")
        {
            Logger.LogInformation("正在启动更新安装程序：{}", packagePath);
            Process.Start(new ProcessStartInfo(packagePath) { UseShellExecute = true });
            Settings.LastUpdateStatus = UpdateStatus.UpdateDeployed;
            ClearPendingUpdate();
            return;
        }

        var root = Path.GetFullPath(CommonDirectories.AppPackageRoot);
        var currentDirectory = Path.GetFullPath(Environment.CurrentDirectory);
        if (string.Equals(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                currentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "未检测到独立的程序包目录，当前安装形态无法就地部署更新，请手动下载安装包进行更新。");
        }

        var stagingPath = Path.Combine(UpdateTempPath, "extracted");
        if (Directory.Exists(stagingPath))
        {
            Directory.Delete(stagingPath, true);
        }

        Directory.CreateDirectory(stagingPath);
        Logger.LogInformation("正在解压更新包");
        ZipFile.ExtractToDirectory(packagePath, stagingPath, true);

        var deploymentDirectory = Directory.GetDirectories(stagingPath)
            .FirstOrDefault(x => Path.GetFileName(x).StartsWith("app", StringComparison.Ordinal));
        if (deploymentDirectory == null)
        {
            throw new InvalidOperationException("更新包结构无效：未找到应用部署目录。");
        }

        var deploymentName = Path.GetFileName(deploymentDirectory);
        var uniqueName = MakeUniqueDeploymentName(root, deploymentName);
        if (uniqueName != deploymentName)
        {
            var renamedPath = Path.Combine(stagingPath, uniqueName);
            Directory.Move(deploymentDirectory, renamedPath);
            UpdateDeploymentNumber(stagingPath, deploymentName, uniqueName);
        }

        Logger.LogInformation("正在部署新的应用版本：{}", uniqueName);
        foreach (var entry in Directory.GetFileSystemEntries(stagingPath))
        {
            var target = Path.Combine(root, Path.GetFileName(entry));
            if (Directory.Exists(entry))
            {
                CopyDirectory(entry, target);
            }
            else
            {
                File.Copy(entry, target, true);
            }
        }

        if (OperatingSystem.IsLinux() && AppBase.Current.PackagingType == "folder")
        {
            using var proc = Process.Start(new ProcessStartInfo("chmod",
            [
                "+x",
                Path.GetFullPath(Path.Combine(root, "ClassFabric"))
            ]));
            var task = proc?.WaitForExitAsync();
            if (task != null)
            {
                await task;
            }
        }

        await File.WriteAllTextAsync(Path.Combine(root, uniqueName, ".current"), "");
        await File.WriteAllTextAsync(Path.Combine(Environment.CurrentDirectory, ".destroy"), "");
        Settings.LastUpdateStatus = UpdateStatus.UpdateDeployed;
        ClearPendingUpdate();
        Logger.LogInformation("部署成功");
        await RemoveDownloadedFiles(false);
    }

    private static string MakeUniqueDeploymentName(string root, string deploymentName)
    {
        if (!Directory.Exists(Path.Combine(root, deploymentName)))
        {
            return deploymentName;
        }

        var separator = deploymentName.LastIndexOf('-');
        var prefix = separator >= 0 ? deploymentName[..separator] : deploymentName;
        var number = separator >= 0 && int.TryParse(deploymentName[(separator + 1)..], out var parsed)
            ? parsed
            : 0;
        string candidate;
        do
        {
            number++;
            candidate = $"{prefix}-{number}";
        } while (Directory.Exists(Path.Combine(root, candidate)));

        return candidate;
    }

    private void UpdateDeploymentNumber(string stagingPath, string deploymentName, string uniqueName)
    {
        var filesJsonPath = Path.Combine(stagingPath, "files.json");
        if (!File.Exists(filesJsonPath))
        {
            return;
        }

        try
        {
            var fileMap = ConfigureFileHelper.LoadConfigUnWrapped<FileMap>(filesJsonPath, false);
            var separator = uniqueName.LastIndexOf('-');
            if (separator >= 0)
            {
                fileMap.Variables["number"] = uniqueName[(separator + 1)..];
            }

            foreach (var (_, component) in fileMap.Components)
            {
                component.Root = component.Root.Replace(deploymentName, uniqueName, StringComparison.Ordinal);
            }

            ConfigureFileHelper.SaveConfig(filesJsonPath, fileMap);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "更新部署目录编号失败，后续差量更新可能回退为全量下载。");
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), true);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
