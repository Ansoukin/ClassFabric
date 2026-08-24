using System;

namespace ClassFabric.Models.Logging;

public class LoggingScope(Action onDispose) : IDisposable
{
    public void Dispose()
    {
        onDispose?.Invoke();
    }
}