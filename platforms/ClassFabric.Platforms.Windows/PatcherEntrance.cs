using HarmonyLib;

namespace ClassFabric.Platform.Windows;

public static class PatcherEntrance
{
    public static void InstallPatchers()
    {
        var harmony = new Harmony("cn.classfabric.app.patchers");
        harmony.PatchAll();
    }
}