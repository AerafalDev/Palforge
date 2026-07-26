using System.Runtime.CompilerServices;

namespace Palforge.Unreal.Threading;

public readonly struct GameThreadAwaiter : INotifyCompletion
{
    public bool IsCompleted =>
        GameThread.IsCurrent;

    public void GetResult()
    {
    }

    public void OnCompleted(Action continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        GameThread.Schedule(continuation);
    }
}