using System;
using System.Linq;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using TimeService = ClassIsland.Services.ExactTimeService;

namespace ClassIsland.ViewModels.SettingsPages;

public partial class ClockSettingsViewModel : ObservableObject
{
    public SettingsService SettingsService { get; }
    public IExactTimeService ExactTimeService { get; }
    public ILessonsService LessonsService { get; }

    [ObservableProperty] private DateTime _currentTime = DateTime.Now;

    /// <summary>主源下拉框当前选中的下标；预置源占 0..N-1，"自定义…"固定在最后。</summary>
    [ObservableProperty] private int _selectedTimeServerPreset;

    /// <summary>主源选"自定义…"时的手输地址。</summary>
    [ObservableProperty] private string _customTimeServerText = "";

    /// <summary>主源选了"自定义…"才展开输入框。</summary>
    public bool IsCustomServerInputVisible => SelectedTimeServerPreset == CustomServerIndex;

    public int CustomServerIndex => TimeService.PresetServerHosts.Length;

    /// <summary>加载期间禁止写回设置，避免半初始化状态写脏存储。</summary>
    private bool _isLoadingServerOptions;

    public ClockSettingsViewModel(SettingsService settingsService, IExactTimeService exactTimeService,
        ILessonsService lessonsService)
    {
        SettingsService = settingsService;
        ExactTimeService = exactTimeService;
        LessonsService = lessonsService;
        LoadServerOptions();
    }

    /// <summary>
    /// 把设置里存的服务器地址映射回下拉框。存储只保留用户选定的源；
    /// 备用容灾不需要用户操心——同步时服务层会自动把其余预置源与遗留的自定义条目当作备用源。
    /// </summary>
    private void LoadServerOptions()
    {
        _isLoadingServerOptions = true;
        try
        {
            var primaryHost = TimeService.ParseServerList(SettingsService.Settings.ExactTimeServer)
                .FirstOrDefault() ?? TimeService.PresetServerHosts[0];

            var presetIndex = Array.FindIndex(TimeService.PresetServerHosts,
                x => string.Equals(x, primaryHost, StringComparison.OrdinalIgnoreCase));
            if (presetIndex >= 0)
            {
                SelectedTimeServerPreset = presetIndex;
            }
            else
            {
                SelectedTimeServerPreset = CustomServerIndex;
                CustomTimeServerText = primaryHost;
            }
        }
        finally
        {
            _isLoadingServerOptions = false;
        }
    }

    /// <summary>把界面状态写回设置。只存用户选定的源；留空时由服务层回退默认源。</summary>
    private void RebuildServerList()
    {
        if (_isLoadingServerOptions)
        {
            return;
        }

        SettingsService.Settings.ExactTimeServer = SelectedTimeServerPreset == CustomServerIndex
            ? CustomTimeServerText.Trim()
            : TimeService.PresetServerHosts.ElementAtOrDefault(SelectedTimeServerPreset) ?? "";
    }

    partial void OnSelectedTimeServerPresetChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustomServerInputVisible));
        RebuildServerList();
    }

    partial void OnCustomTimeServerTextChanged(string value)
    {
        RebuildServerList();
    }
}
