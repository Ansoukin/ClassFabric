using ClassFabric.Core.Abstractions.Services.SpeechService;
using ClassFabric.Shared.Abstraction.Services;

namespace ClassFabric.Services.SpeechService;

public class BlankSpeechService : ISpeechService
{
    public void EnqueueSpeechQueue(string text)
    {
    }

    public void ClearSpeechQueue()
    {
    }
}