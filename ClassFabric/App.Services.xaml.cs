using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ClassFabric.Controls.ActionSettingsControls;
using ClassFabric.Controls.AttachedSettingsControls;
using ClassFabric.Controls.AuthorizeProvider;
using ClassFabric.Controls.Components;
using ClassFabric.Controls.EditMode;
using ClassFabric.Controls.NotificationProviders;
using ClassFabric.Controls.ProfileTransferProviders;
using ClassFabric.Controls.RuleSettingsControls;
using ClassFabric.Controls.SpeechProviderSettingsControls;
using ClassFabric.Controls.TriggerSettingsControls;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Abstractions.Services.Management;
using ClassFabric.Core.Abstractions.Services.Metadata;
using ClassFabric.Core.Abstractions.Services.SpeechService;
using ClassFabric.Core.Controls.Ruleset;
using ClassFabric.Core.Enums.Profile;
using ClassFabric.Core.Extensions.Registry;
using ClassFabric.Core.Helpers;
using ClassFabric.Core.Models.Logging;
using ClassFabric.Core.Models.Ruleset;
using ClassFabric.Core.Models.XamlTheme;
using ClassFabric.Helpers.ProfileTransferHelpers;
using ClassFabric.Models.Actions;
using ClassFabric.Models.Automation.Triggers;
using ClassFabric.Models.Rules;
using ClassFabric.Platforms.Abstraction;
using ClassFabric.Platforms.Abstraction.Services;
using ClassFabric.Services;
using ClassFabric.Services.AppUpdating;
using ClassFabric.Services.Automation.Actions;
using ClassFabric.Services.Automation.Triggers;
using ClassFabric.Services.Logging;
using ClassFabric.Services.Management;
using ClassFabric.Services.Metadata;
using ClassFabric.Services.NotificationProviders;
using ClassFabric.Services.SpeechService;
using ClassFabric.ViewModels;
using ClassFabric.ViewModels.EditMode;
using ClassFabric.ViewModels.SettingsPages;
using ClassFabric.Views;
using ClassFabric.Views.SettingPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.EventLog;

namespace ClassFabric;

public partial class App
{
    private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<SettingsService>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<ClassFabric.Services.Automation.PlatformTools.InputSimulationService>();
        services.AddSingleton<ClassFabric.Services.Automation.PlatformTools.PowerService>();
        services.AddSingleton<ClassFabric.Services.Automation.PlatformTools.ScreenCaptureService>();
        services.AddSingleton<ClassFabric.Services.Automation.PlatformTools.ElevationService>();
        services.AddSingleton<ClassFabric.Services.Automation.PlatformTools.ActionFlowCoordinatorService>();
        services.AddSingleton<ITaskBarIconService, TaskBarIconService>();
        // services.AddSingleton<WallpaperPickingService>();
        services.AddSingleton<INotificationHostService, NotificationHostService>();
        services.AddSingleton<INotificationWorkerService, NotificationWorkerService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<MiniInfoProviderHostService>();
        services.AddSingleton<IWeatherService, WeatherService>();
        services.AddSingleton<FileFolderService>();
        services.AddSingleton<IAttachedSettingsHostService, AttachedSettingsHostService>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<ISplashService, SplashService>();
        services.AddSingleton<IHangService, HangService>();
        services.AddSingleton<ConsoleService>();
        //services.AddHostedService<BootService>();
        services.AddSingleton<DiagnosticService>();
        services.AddSingleton<IManagementService, ManagementService>();
        services.AddSingleton<AppLogService>();
        services.AddSingleton<IComponentsService, ComponentsService>();
        services.AddSingleton<ILessonsService, LessonsService>();
        services.AddSingleton<IUriNavigationService, UriNavigationService>();
        services.AddHostedService<MemoryWatchDogService>();
        services.AddSingleton<IPluginService, PluginService>();
        services.AddSingleton<IPluginMarketService, PluginMarketService>();
        services.AddSingleton<IRulesetService, RulesetService>();
        services.AddSingleton<IActionService, ActionService>();
        services.AddSingleton<IWindowRuleService, WindowRuleService>();
        services.AddSingleton<IAutomationService, AutomationService>();
        services.AddSingleton<ISpeechService>(GetSpeechService);
        services.AddSingleton<IExactTimeService, ExactTimeService>();
        //services.AddSingleton(typeof(ApplicationCommand), ApplicationCommand);
        services.AddSingleton<IProfileAnalyzeService, ProfileAnalyzeService>();
        services.AddSingleton<IIpcService, IpcService>();
        services.AddSingleton<IAuthorizeService, AuthorizeService>();
        services.AddSingleton<UriTriggerHandlerService>();
        services.AddSingleton<SignalTriggerHandlerService>();
        services.AddSingleton<TrayMenuTriggerHandlerService>();
        services.AddSingleton<ClassFabric.Services.Automation.Triggers.ActionInProgressTriggerHandlerService>();
        services.AddSingleton<ClassFabric.Services.Automation.Rules.ToolsRulesetHandlerService>();
        services.AddSingleton<IAnnouncementService, AnnouncementService>();
        services.AddSingleton<ILocationService>(PlatformServices.LocationService);
        services.AddSingleton<IXamlThemeService, XamlThemeService>();
        services.AddSingleton<IAudioService, AudioService>();
        services.AddSingleton<ITutorialService, TutorialService>();
        services.AddSingleton<IRefreshingService, RefreshingService>();
        // ViewModels
        services.AddTransient<ProfileSettingsViewModel>();
        services.AddTransient<DevPortalViewModel>();
        services.AddTransient<AppLogsViewModel>();
        services.AddTransient<WelcomeViewModel>();
        services.AddTransient<ClassChangingViewModel>();
        services.AddTransient<DataTransferViewModel>();
        services.AddTransient<ScreenshotHelperViewModel>();
        services.AddTransient<EditModeViewModel>();
        services.AddTransient<TutorialEditorViewModel>();
        services.AddTransient<TutorialCenterViewModel>();
        // ViewModels/SettingsPages
        services.AddTransient<GeneralSettingsViewModel>();
        services.AddTransient<ClockSettingsViewModel>();
        services.AddTransient<AdvancedSettingsViewModel>();
        services.AddTransient<AboutSettingsViewModel>();
        services.AddTransient<AppearanceSettingsViewModel>();
        services.AddTransient<ComponentsSettingsViewModel>();
        services.AddTransient<NotificationSettingsViewModel>();
        services.AddTransient<WindowSettingsViewModel>();
        services.AddTransient<WeatherSettingsViewModel>();
        services.AddTransient<AutomationSettingsViewModel>();
        services.AddTransient<PluginsSettingsPageViewModel>();
        services.AddTransient<StorageSettingsViewModel>();
        services.AddTransient<ErrorSettingsViewModel>();
        services.AddTransient<ThemesSettingsViewModel>();
        services.AddTransient<UpdateSettingsPageViewModel>();
        services.AddTransient<DebugPageViewModel>();
        services.AddTransient<RefreshingSettingsViewModel>();
        // Views
        services.AddTransient<ITopmostEffectPlayer>(x => x.GetRequiredService<TopmostEffectWindow>());
        services.AddSingleton<MainWindow>();
        // services.AddTransient<SplashWindowBase, SplashWindow>();
        // services.AddTransient<FeatureDebugWindow>();
        services.AddSingleton<TopmostEffectWindow>();
        services.AddSingleton<VerticalScheduleWindow>();
        services.AddSingleton<VerticalScheduleService>();
        services.AddSingleton<AppLogsWindow>();
        services.AddSingleton<SettingsWindowNew>();
        services.AddSingleton<ProfileSettingsWindow>();
        services.AddTransient<ClassPlanDetailsWindow>();
        services.AddTransient<WindowRuleDebugWindow>();
        // services.AddTransient<ConfigErrorsWindow>();
        services.AddTransient<TimeAdjustmentWindow>();
        // services.AddTransient<ExcelExportWindow>();
        services.AddTransient<DevPortalWindow>();
        services.AddTransient<WelcomeWindow>();
        services.AddTransient<DataTransferWindow>();
        services.AddTransient<ScreenshotHelperWindow>();
        services.AddSingleton<EditModeView>();
        services.AddTransient<TutorialEditorWindow>();
        services.AddSingleton<TutorialCenterWindow>();
        services.AddTransient<TutorialControllerWindow>();
        services.AddTransient<ISplashProvider, SplashWindow>();
        // 设置页面分组
        services.AddSettingsPageGroup("classfabric.general", "\uef27", "通用");
        services.AddSettingsPageGroup("classfabric.mainwindow", "\uec85", "主界面");
        // 设置页面
        services.AddSettingsPage<GeneralSettingsPage>();
        services.AddSettingsPage<ClockSettingsPage>();
        services.AddSettingsPage<StorageSettingsPage>();
        services.AddSettingsPage<PrivacySettingsPage>();
        services.AddSettingsPage<RefreshingSettingsPage>();
        services.AddSettingsPage<AdvancedSettingsPage>();
        services.AddSettingsPage<ComponentsSettingsPage>();
        services.AddSettingsPage<AppearanceSettingsPage>();
        services.AddSettingsPage<NotificationSettingsPage>();
        services.AddSettingsPage<WindowSettingsPage>();
        services.AddSettingsPage<WeatherSettingsPage>();
        services.AddSettingsPage<AutomationSettingsPage>();
        if (UpdateService.AllowedPackageTypes.Contains(PackagingType))
        {
            services.AddSettingsPage<UpdateSettingsPage>();
        }
        services.AddSettingsPage<PluginsSettingsPage>();
        services.AddSettingsPage<ThemesSettingsPage>();
        services.AddSettingsPage<TestSettingsPage>();
        services.AddSettingsPage<DebugPage>();
        // services.AddSettingsPage<DebugBrushesSettingsPage>();
        services.AddSettingsPage<AboutSettingsPage>();
        services.AddSettingsPage<ManagementSettingsPage>();
        services.AddSettingsPage<ManagementCredentialsSettingsPage>();
        services.AddSettingsPage<ManagementPolicySettingsPage>();
        services.AddSettingsPage<ErrorSettingsPage>();
        // 主界面组件
        services.AddComponent<TextComponent, TextComponentSettingsControl>();
        services.AddComponent<SeparatorComponent>();
        services.AddComponent<ScheduleComponent, ScheduleComponentSettingsControl>();
        services.AddComponent<DateComponent>();
        services.AddComponent<ClockComponent, ClockComponentSettingsControl>();
        services.AddComponent<WeatherComponent, WeatherComponentSettingsControl>();
        services.AddComponent<CountDownComponent, CountDownComponentSettingsControl>();
        services.AddComponent<SlideComponent, SlideComponentSettingsControl>();
        services.AddComponent<RollingComponent, RollingComponentSettingsControl>();
        services.AddComponent<GroupComponent>();
        services.AddComponent<StackComponent>();
        // 提醒提供方
        services.AddNotificationProvider<ClassNotificationProvider, ClassNotificationProviderSettingsControl>();
        services.AddNotificationProvider<AfterSchoolNotificationProvider, AfterSchoolNotificationProviderSettingsControl>();
        services.AddNotificationProvider<WeatherNotificationProvider, WeatherNotificationProviderSettingsControl>();
        services.AddNotificationProvider<ManagementNotificationProvider>();
        services.AddNotificationProvider<ActionNotificationProvider>();
        // // Transients
        // services.AddTransient<ExcelImportWindow>();
        // services.AddTransient<WallpaperPreviewWindow>();
        // Logging
        services.AddLogging(builder =>
        {
            LogMaskingHelper.Rules.Add(new LogMaskRule(new(@"(latitude=)(\d*\.?\d*)"), 2));
            LogMaskingHelper.Rules.Add(new LogMaskRule(new(@"(longitude=)(\d*\.?\d*)"), 2));

            builder.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Error);
            builder.AddConsoleFormatter<ClassFabricConsoleFormatter, ConsoleFormatterOptions>();
            builder.AddConsole(console => { console.FormatterName = "classfabric"; });
            var debug = false;
#if DEBUG
            debug = true;
#endif
            if (ApplicationCommand.Verbose || debug)
            {
                builder.SetMinimumLevel(LogLevel.Trace);
            }
        });
        services.AddSingleton<ILoggerProvider, AppLoggerProvider>();
        services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
        // AttachedSettings
        services.AddAttachedSettingsControl<AfterSchoolNotificationAttachedSettingsControl>();
        services.AddAttachedSettingsControl<ClassNotificationAttachedSettingsControl>();
        services.AddAttachedSettingsControl<LessonControlAttachedSettingsControl>();
        services.AddAttachedSettingsControl<WeatherNotificationAttachedSettingsControl>();
        // 触发器
        services.AddTrigger<RulesetChangedTrigger>();
        services.AddTrigger<SignalTrigger, SignalTriggerSettingsControl>();
        services.AddTrigger<UriTrigger, UriTriggerSettingsControl>();
        services.AddTrigger<TrayMenuTrigger, TrayMenuTriggerSettingsControl>();
        services.AddTrigger<CronTrigger, CronTriggerSettingsControl>();
        services.AddTrigger<AppStartupTrigger>();
        services.AddTrigger<AppStoppingTrigger>();
        services.AddTrigger<OnClassTrigger>();
        services.AddTrigger<OnBreakingTimeTrigger>();
        services.AddTrigger<OnAfterSchoolTrigger>();
        services.AddTrigger<CurrentTimeStateChangedTrigger>();
        services.AddTrigger<PreTimePointTrigger, PreTimePointTriggerSettingsControl>();
        services.AddTrigger<ActionInProgressTrigger, ActionInProgressTriggerSettingsControl>();
        // 规则
        services.AddRule("classfabric.test.true", "总是为真", onHandle: _ => true);
        services.AddRule("classfabric.test.false", "总是为假", onHandle: _ => false);
        services.AddRule<ProcessRunningRuleSettings, ProcessRunningRuleSettingsControl>("classfabric.tools.processRunning", "程序正在运行", "\uE7C3");
        services.AddRule<UsingClassPlanRuleSettings, UsingClassPlanRuleSettingsControl>("classfabric.tools.usingClassPlan", "正在使用课程表", "\uE719");
        services.AddRule<UsingTimeLayoutRuleSettings, UsingTimeLayoutRuleSettingsControl>("classfabric.tools.usingTimeLayout", "正在使用时间表", "\uE823");
        services.AddRule<InTimePeriodRuleSettings, InTimePeriodRuleSettingsControl>("classfabric.tools.inTimePeriod", "处于时间段内", "\uE823");
        services.AddRule<StringMatchingSettings, RulesetStringMatchingSettingsControl>("classfabric.windows.className", "前台窗口类名", "\uF4A2");
        services.AddRule<StringMatchingSettings, RulesetStringMatchingSettingsControl>("classfabric.windows.text", "前台窗口标题", "\uF26B");
        services.AddRule<WindowStatusRuleSettings, WindowStatusRuleSettingsControl>("classfabric.windows.status", "前台窗口状态是", "\uEC83");
        services.AddRule<StringMatchingSettings, RulesetStringMatchingSettingsControl>("classfabric.windows.processName", "前台窗口进程", "\uF488");
        services.AddRule<CurrentSubjectRuleSettings, CurrentSubjectRuleSettingsControl>("classfabric.lessons.currentSubject", "科目是", "\uE215");
        services.AddRule<CurrentSubjectRuleSettings, CurrentSubjectRuleSettingsControl>("classfabric.lessons.nextSubject", "下节课科目是", "\uE217");
        services.AddRule<CurrentSubjectRuleSettings, CurrentSubjectRuleSettingsControl>("classfabric.lessons.previousSubject", "上节课科目是", "\uE226");
        services.AddRule<TimeStateRuleSettings, TimeStateRuleSettingsControl>("classfabric.lessons.timeState", "当前时间状态是", "\uE4C4");
        services.AddRule<CurrentWeatherRuleSettings, CurrentWeatherRuleSettingsControl>("classfabric.weather.currentWeather", "当前天气是", "\uE4DC");
        services.AddRule<CurrentWeatherRuleSettings, CurrentWeatherRuleSettingsControl>("classfabric.weather.tomorrowWeather", "明天天气是", "\uE4DC");
        services.AddRule<StringMatchingSettings, RulesetStringMatchingSettingsControl>("classfabric.weather.hasWeatherAlert", "存在气象预警", "\uF431");
        services.AddRule<RainTimeRuleSettings, RainTimeRuleSettingsControl>("classfabric.weather.rainTime", "距离降水开始/结束还剩", "\uF43F");
        services.AddRule<SunRiseSetRuleSettings, SunRiseSetRuleSettingsControl>("classfabric.weather.sunRiseSet", "是否日出/日落", "\uE150");
        // 行动提供方
        services.AddAction<SignalTriggerSettings, BroadcastSignalActionSettingsControl>("classfabric.broadcastSignal", "广播信号", "\uE561");
        services.AddAction<RunAction, RunActionSettingsControl>();
        services.AddAction<NotificationAction, NotificationActionSettingsControl>();
        services.AddAction<SleepAction, SleepActionSettingsControl>();
        services.AddAction<ModifyAppSettingsAction, ModifyAppSettingsActionSettingsControl>();
        services.AddAction<WeatherNotificationAction, WeatherNotificationActionSettingControl>();
        services.AddAction<AppQuitAction>();
        services.AddAction<AppRestartAction, AppRestartActionSettingsControl>();
        services.AddAction<Services.Automation.Actions.SimulateHotkeyAction, Controls.ActionSettingsControls.SimulateHotkeyActionSettingsControl>();
        services.AddAction<Services.Automation.Actions.ShutdownTimerAction, Controls.ActionSettingsControls.ShutdownTimerActionSettingsControl>();
        // B 包行动注册（输入/电源/文件工具/集成/工作流族）
        services.AddAction<SimulateKeyCombinationAction, SimulateKeyCombinationActionSettingsControl>();
        services.AddAction<SimulateKeyboardAction, SimulateKeyboardActionSettingsControl>();
        services.AddAction<SimulateMouseAction, SimulateMouseActionSettingsControl>();
        services.AddAction<TypeContentAction, TypeContentActionSettingsControl>();
        services.AddAction<WindowOperationAction, WindowOperationActionSettingsControl>();
        services.AddAction<AdvancedShutdownAction, AdvancedShutdownActionSettingsControl>();
        services.AddAction<CancelShutdownAction, CancelShutdownActionSettingsControl>();
        services.AddAction<LockScreenAction, LockScreenActionSettingsControl>();
        services.AddAction<SystemSleepAction, SystemSleepActionSettingsControl>();
        services.AddAction<ImmediateRestartAction, ImmediateRestartActionSettingsControl>();
        services.AddAction<ImmediateShutdownAction, ImmediateShutdownActionSettingsControl>();
        services.AddAction<CopyPathAction, CopyPathActionSettingsControl>();
        services.AddAction<MovePathAction, MovePathActionSettingsControl>();
        services.AddAction<DeletePathAction, DeletePathActionSettingsControl>();
        services.AddAction<ScreenshotAction, ScreenshotActionSettingsControl>();
        services.AddAction<ShowNotificationAction, ShowNotificationActionSettingsControl>();
        services.AddAction<KillProcessAction, KillProcessActionSettingsControl>();
        services.AddAction<ClearNotificationsAction, ClearNotificationsActionSettingsControl>();
        services.AddAction<RestartAsAdminAction, RestartAsAdminActionSettingsControl>();
        services.AddAction<LoadTempClassPlanAction, LoadTempClassPlanActionSettingsControl>();
        services.AddAction<OpenSettingsAction, OpenSettingsActionSettingsControl>();
        services.AddAction<OpenProfileEditorAction, OpenProfileEditorActionSettingsControl>();
        services.AddAction<OpenClassSwapAction, OpenClassSwapActionSettingsControl>();
        services.AddAction<FlowConfirmationAction, FlowConfirmationActionSettingsControl>();
        services.AddAction<TriggerCustomTriggerAction, TriggerCustomTriggerActionSettingsControl>();
        services.AddAction<ToggleWorkflowAction, ToggleWorkflowActionSettingsControl>();

        // 认证提供方
        services.AddAuthorizeProvider<PasswordAuthorizeProvider>();
        // 语音提供方
        if (System.OperatingSystem.IsWindows()) {
            services.AddSpeechProvider<SystemSpeechService>();
        }
        services.AddSpeechProvider<EdgeTtsService, EdgeTtsSpeechServiceSettingsControl>();
        services.AddSpeechProvider<GptSoVitsService, GptSovitsSpeechServiceSettingsControl>();
        // 天气图标模板
        services.AddWeatherIconTemplate("classfabric.weatherIcons.lucide", "Lucide（默认）", (this.FindResource("LucideWeatherIconTemplate") as IDataTemplate)!);
        services.AddWeatherIconTemplate("classfabric.weatherIcons.fluentDesign", "Fluent Design", (this.FindResource("FluentDesignWeatherIconTemplate") as IDataTemplate)!);
        services.AddWeatherIconTemplate("classfabric.weatherIcons.sfSymbols", "SF Symbols", (this.FindResource("SFSymbolsWeatherIconTemplate") as IDataTemplate)!);
        services.AddWeatherIconTemplate("classfabric.weatherIcons.simpleText", "纯文本", (this.FindResource("SimpleTextWeatherIconTemplate") as IDataTemplate)!);
        // 档案迁移提供方
        services.AddProfileTransferProvider<CsesImportProvider>("classfabric.profileTransfer.import.cses", "从 CSES 导入", ProfileTransferProviderType.Import, "\ue6cb");
        services.AddProfileTransferProvider<ClassFabric1ImportProvider>("classfabric.profileTransfer.import.legacyV1", "从 ClassFabric 1.x 导入", ProfileTransferProviderType.Import, "bitmap(avares://ClassFabric/Assets/AppLogo.png)");
        services.AddProfileTransferProvider<ClassWidgets1ImportProvider>("classfabric.profileTransfer.import.classWidgets", "从 Class Widgets 1.2 导入", ProfileTransferProviderType.Import);
        services.AddProfileTransferProvider("classfabric.profileTransfer.export.cses", "导出到 CSES", ProfileTransferProviderType.Export, CsesExportHelper.CsesExportHandler, "\ue6cb");
        // Themes
        services.AddXamlTheme(new Uri("avares://ClassFabric/XamlThemes/ClassicTheme/Styles.axaml"), new ThemeManifest()
        {
            Id = "classfabric.classic",
            Name = "经典",
            Description = "ClassFabric 的经典外观。",
            Banner = "avares://ClassFabric/Assets/XamlThemePreviews/classfabric.classic.png",
            Author = "ClassFabric",
            Url = "https://github.com/ClassIsland/ClassIsland"
        });
        services.AddXamlTheme(new Uri("avares://ClassFabric/XamlThemes/FluentTheme/Styles.axaml"), new ThemeManifest()
        {
            Id = "classfabric.fluent",
            Name = "Fluent",
            Description = "焕然一新的 ClassFabric 外观。",
            Banner = "avares://ClassFabric/Assets/XamlThemePreviews/classfabric.fluent.png",
            Author = "ClassFabric",
            Url = "https://github.com/ClassIsland/ClassIsland",
            VerticalSafeAreaPx = 20
        });
        // 教程
        // services.AddTutorialGroupByUri(new Uri("avares://ClassFabric/Assets/Tutorials/classfabric.test.json"));
        // services.AddTutorialGroupByUri(new Uri("avares://ClassFabric/Assets/Tutorials/classfabric.sp.json"));
        services.AddTutorialGroupByUri(new Uri("avares://ClassFabric/Assets/Tutorials/classfabric.getStarted.json"));
        // Plugins
        if (!ApplicationCommand.Safe && string.IsNullOrWhiteSpace(ApplicationCommand.ImportV1) && string.IsNullOrWhiteSpace(ApplicationCommand.ImportV2))
        {
            PluginService.InitializePlugins(context, services);
        }
    }
}