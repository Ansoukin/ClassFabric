using System;
using ClassFabric.Shared.IPC.Abstractions.Services;

namespace ClassFabric.Services;

public class FooService : IFooService
{
    public void DoSomething()
    {
        Console.WriteLine("Foobar");
    }
}