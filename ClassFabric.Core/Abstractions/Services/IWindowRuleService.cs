using ClassFabric.Platforms.Abstraction.Models;

namespace ClassFabric.Core.Abstractions.Services;

/// <summary>
/// 窗口规则服务，处理窗口相关的规则。
/// </summary>
public interface IWindowRuleService
{
    /// <summary>
    /// 当焦点窗口发生变化时触发
    /// </summary>
    event EventHandler<ForegroundWindowChangedEventArgs>? ForegroundWindowChanged;

    /// <summary>
    /// 判断前台窗口是否属于 ClassFabric。
    /// </summary>
    bool IsForegroundWindowClassFabric();
}