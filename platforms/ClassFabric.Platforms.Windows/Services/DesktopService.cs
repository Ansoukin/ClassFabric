using ClassFabric.Core;
using ClassFabric.Helpers;
using ClassFabric.Platforms.Abstraction.Services;
using WindowsShortcutFactory;

namespace ClassFabric.Platform.Windows.Services;

public class DesktopService : IDesktopService
{
    public bool IsAutoStartEnabled
    {
        get => File.Exists(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "ClassFabric.lnk"));
        set
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "ClassFabric.lnk");
            if (value)
            {
                using var shortcut = new WindowsShortcut();
                shortcut.Path = AppBase.ExecutingEntrance;
                shortcut.Arguments = "--autostartup";
                shortcut.WorkingDirectory = Path.GetDirectoryName(AppBase.ExecutingEntrance);
                shortcut.Save(path);
            }
            else
            {
                File.Delete(path);
            }
            
        }
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