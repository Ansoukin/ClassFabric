using ClassIsland.Core;
using ClassIsland.Helpers;
using ClassIsland.Platforms.Abstraction.Services;
using Microsoft.Win32.TaskScheduler;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using WindowsShortcutFactory;

namespace ClassIsland.Platform.Windows.Services;

// 实现 INotifyPropertyChanged：两个自启开关互斥联动时，设置页的 ToggleSwitch 绑定需要属性通知才能同步刷新 UI。
public class DesktopService : IDesktopService, INotifyPropertyChanged
{
    const string AdminStartupTaskName = "ClassIsland.AdminStartup";

    public event PropertyChangedEventHandler? PropertyChanged;

    void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public bool IsAutoStartEnabled
    {
        get => File.Exists(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "ClassIsland.lnk"));
        set
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "ClassIsland.lnk");
            if (value)
            {
                using var shortcut = new WindowsShortcut();
                shortcut.Path = AppBase.ExecutingEntrance;
                shortcut.Arguments = "--autostartup";
                shortcut.WorkingDirectory = Path.GetDirectoryName(AppBase.ExecutingEntrance);
                shortcut.Save(path);
                // 与管理员自启（计划任务）互斥：两者并存会在登录时拉起两个实例。
                DeleteAdminStartupTask();
            }
            else
            {
                File.Delete(path);
            }

            Raise();
            Raise(nameof(IsAdminStartupEnabled));
        }
    }

    public bool IsAdminStartupEnabled
    {
        get
        {
            using var taskService = new TaskService();
            return taskService.GetTask(AdminStartupTaskName) != null;
        }
        set
        {
            if (value)
            {
                using var taskService = new TaskService();
                if (taskService.GetTask(AdminStartupTaskName) != null)
                    return;
                var definition = taskService.NewTask();
                definition.Triggers.Add(new LogonTrigger { UserId = Environment.UserName });
                definition.Actions.Add(new ExecAction(AppBase.ExecutingEntrance, null,
                    Path.GetDirectoryName(AppBase.ExecutingEntrance)));
                // 计划任务默认电池供电时停跑、限时执行，自启场景都要关掉；RunLevel 决定以管理员身份拉起。
                definition.Settings.RunOnlyIfIdle = false;
                definition.Settings.StopIfGoingOnBatteries = false;
                definition.Settings.DisallowStartIfOnBatteries = false;
                definition.Settings.ExecutionTimeLimit = TimeSpan.Zero;
                definition.Principal.RunLevel = TaskRunLevel.Highest;
                taskService.RootFolder.RegisterTaskDefinition(AdminStartupTaskName, definition,
                    TaskCreation.CreateOrUpdate, null, null, TaskLogonType.InteractiveToken);
                // 与常规自启互斥：两者并存会在登录时拉起两个实例。
                IsAutoStartEnabled = false;
            }
            else
            {
                DeleteAdminStartupTask();
            }

            Raise();
            Raise(nameof(IsAutoStartEnabled));
        }
    }

    public bool IsRunningAsAdmin =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    static void DeleteAdminStartupTask()
    {
        using var taskService = new TaskService();
        // exceptionOnNotExists=false：常规自启开关每次开启都会走这里清理互斥任务，任务本就不存在时不应抛错。
        taskService.RootFolder.DeleteTask(AdminStartupTaskName, false);
    }

    public bool IsUrlSchemeRegistered{
        get => UriProtocolRegisterHelper.IsRegistered();
        set
        {
            if (value)
            {
                UriProtocolRegisterHelper.Register();
            }
            else
            {
                UriProtocolRegisterHelper.UnRegister();
            }
        }
    }
}
