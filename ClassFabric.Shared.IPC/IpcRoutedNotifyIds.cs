namespace ClassFabric.Shared.IPC;

/// <summary>
/// 存储了 ClassFabric 内置的跨进程通信路由通知标识符。
/// </summary>
public static class IpcRoutedNotifyIds
{
    /// <summary>
    /// 上课事件通知标识符
    /// </summary>
    public const string OnClassNotifyId = "classfabric.lessonsService.onClass";

    /// <summary>
    /// 课间休息事件通知标识符
    /// </summary>
    public const string OnBreakingTimeNotifyId = "classfabric.lessonsService.onBreakingTime";

    /// <summary>
    /// 放学事件通知标识符
    /// </summary>
    public const string OnAfterSchoolNotifyId = "classfabric.lessonsService.onAfterSchool";

    /// <summary>
    /// 当前时间点状态通知标识符
    /// </summary>
    public const string CurrentTimeStateChangedNotifyId = "classfabric.lessonsService.currentTimeStateChanged";
}