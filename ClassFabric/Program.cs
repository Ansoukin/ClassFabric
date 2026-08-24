using ClassFabric.Models;
using System;
using System.Collections.Generic;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using ClassFabric;
using ClassFabric.Core;
using ClassFabric.Core.Converters;
using ClassFabric.Core.Enums;
using ClassFabric.Extensions;
using ClassFabric.Services;
using ClassFabric.Shared.Helpers;
using ClassFabric.Shared.IPC;
using ClassFabric.Shared.IPC.Abstractions.Services;
using dotnetCampus.Ipc.CompilerServices.GeneratedProxies;
using HotAvalonia;
using System.Diagnostics;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Models.Tutorial;
using ClassFabric.Core.Services;
using ClassFabric.Shared.JsonConverters;
using ClassFabric.Shared.Models.Profile;
using MoonSharp.Interpreter;

namespace ClassFabric;

public static class Program
{
    [STAThread]
    public static Func<App> AppEntry(string[] args, Action? postInit = null)
    {
        AppDomain.CurrentDomain.UnhandledException += DiagnosticService.ProcessDomainUnhandledException;
        AppBase.CurrentLifetime = ApplicationLifetime.EarlyLoading;
        
        ConfigureFileHelper.SerializerOptions.Converters.Add(new ColorHexJsonConverter());
        ConfigureFileHelper.SerializerOptions.Converters.Add(new GuidEmptyFallbackConverter());
        ConfigureFileHelper.SerializerOptions.Converters.Add(new OSPlatformConverter_Json());

        var command = new RootCommand
        {
            new Option<string>(["--updateReplaceTarget", "-urt"], "更新时要替换的文件"),
            new Option<string>(["--updateDeleteTarget", "-udt"], "更新完成要删除的文件"),
            new Option<string>(["--uri"], "启动时要导航到的Uri"),
            new Option<bool>(["--waitMutex", "-m"], "重复启动应用时，等待上一个实例退出而非直接退出应用。"),
            new Option<bool>(["--quiet", "-q"], "静默启动，启动时不显示Splash，并且启动后10秒内不显示任何通知。"),
            new Option<bool>(["-prevSessionMemoryKilled", "-psmk"], "上个会话因MLE结束。"),
            new Option<bool>(["-disableManagement", "-dm"], "在本次会话禁用集控。"),
            new Option<string>(["-externalPluginPath", "-epp"], "外部插件路径"),
            new Option<bool>(["--verbose", "-v"], "启用详细输出"),
            new Option<bool>(["--showOssWatermark", "-ossw"], "显示开源地址水印"),
            new Option<bool>(["--recovery", "-r"], "启动时进入恢复模式"),
            new Option<bool>(["--diagnostic", "-d"], "启用诊断模式(包括详细输出)，并在桌面上生成一份诊断数据"),
            new Option<bool>(["--safe", "-s"], "启用安全模式"),
            new Option<bool>(["--skip-oobe", "-so"], "跳过 OOBE 启动"),
            new Option<string>(["--importV1"], "指定要导入的 ClassFabric 1.7 配置目录"), 
            new Option<string>(["--importV2"], "指定要导入的 ClassFabric 2.x 配置目录"),
            new Option<string>(["--importEntries"], "指定要导入的 ClassFabric 1.7 配置信息"),
            new Option<bool>(["--importComplete"], "启动时显示导入完成窗口"),
            new Option<bool>(["--importV1Complete"], "从 ClassFabric 1 导入成功"),
            new Option<bool>(["--refreshing"], "应用将继续翻新向导"),
            new Option<bool>(["--onboarding"], "应用将继续迎新向导"),
            new Option<bool>(["--autostartup", "-au"], "自启动模式，检测到程序已运行时直接退出"),
        };
        command.Handler = CommandHandler.Create((ApplicationCommand c) => { App.ApplicationCommand = c; });
        command.Invoke(args);
        
        GlobalStorageService.InitializeGlobalStorage();

        if (App.ApplicationCommand.Diagnostic)
        {
            App.ApplicationCommand.Verbose = true;
            // TODO: 实现 AllocConsole
            // AllocConsole();
        }

        // 修复特定情况下无法正常 DPI 缩放的问题 https://github.com/ClassIsland/ClassIsland/issues/1580
        if (OperatingSystem.IsLinux() && GlobalStorageService.GetValue("IgnoreQtScaling") == "1")
        {
            Environment.SetEnvironmentVariable("QT_SCREEN_SCALE_FACTORS", null);
            Environment.SetEnvironmentVariable("QT_SCALE_FACTORS", null);
        }

        var mutex = new Mutex(true, "Global\\ClassFabric.Lock", out var createNew);

        if (!createNew)
        {
            if (App.ApplicationCommand.WaitMutex)
            {
                try
                {
                    mutex?.WaitOne();
                }
                catch
                {
                    // ignored
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(App.ApplicationCommand.Uri))
                {
                    ProcessUriNavigationAsync().Wait();
                }
            }
        }

        try {
            if (Environment.GetEnvironmentVariable("ClassFabric_ProcessPriority") is { } priorityStr && uint.TryParse(priorityStr, out uint priority))
            {
                SetProcessPriority(priority);
            }
            else SetProcessPriority(2); //If not set or invalid, default to Normal priority (2).
        }
        catch
        {
            // ignore
        }

        UserData.RegisterAssembly(typeof(Program).Assembly);
        UserData.RegisterAssembly(typeof(Tutorial).Assembly);
        UserData.RegisterAssembly(typeof(Profile).Assembly);
        
        return () => new App()
        {
            Mutex = mutex,
            IsMutexCreateNew = createNew
        };
    }
    
    /// <summary>
    /// 用于在发现另一个实例正在运行时，将启动 URI 通过 IPC 发送给已运行实例并退出当前进程。
    /// 此方法在启动参数包含 URI 时被调用以支持单实例的 URI 导航。
    /// </summary>
    private static async Task ProcessUriNavigationAsync()
    {
        try
        {
            var client = new IpcClient();
            await client.Connect();
            var uriSc = client.Provider.CreateIpcProxy<IPublicUriNavigationService>(client.PeerProxy!);
            uriSc.Navigate(new Uri(App.ApplicationCommand.Uri));
            Environment.Exit(0);
        }
        catch
        {
            // ignored
        }
    }
    
    /// <summary>
    /// 设置应用程序的 <see cref="ProcessPriorityClass"/>。
    /// 无效值将回退到 <see cref="ProcessPriorityClass.Normal"/>。
    /// </summary>
    static void SetProcessPriority(uint priority)
    {
        Process.GetCurrentProcess().PriorityClass = priority switch
        {
            0 => ProcessPriorityClass.Idle,
            1 => ProcessPriorityClass.BelowNormal,
            2 => ProcessPriorityClass.Normal,
            3 => ProcessPriorityClass.AboveNormal,
            4 => ProcessPriorityClass.High,
            5 => ProcessPriorityClass.RealTime,
            _ => ProcessPriorityClass.Normal,
        };
    }
}

