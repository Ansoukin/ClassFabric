using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace ClassFabric.Services.Automation.PlatformTools;

/// <summary>
/// 行动流内部协调器：为「触发指定触发器」行动与「行动进行时」触发器提供内存态桥接。
/// 相比上游的磁盘 JSON 轮询，事件直达且无落盘。
/// </summary>
public class ActionFlowCoordinatorService
{
    private readonly Subject<Guid> _flowRequested = new();

    /// <summary>
    /// 携带目标触发器 Id 的请求流。行动进行时触发器订阅此流匹配自身。
    /// </summary>
    public IObservable<Guid> FlowRequested => _flowRequested.AsObservable();

    /// <summary>
    /// 由「触发指定触发器」行动调用，向协调器投递一次指定触发器的执行请求。
    /// </summary>
    public void RequestFlow(Guid triggerId)
    {
        _flowRequested.OnNext(triggerId);
    }
}
