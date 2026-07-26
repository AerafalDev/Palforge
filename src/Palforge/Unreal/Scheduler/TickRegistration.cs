using Palforge.Unreal.Runtime;

namespace Palforge.Unreal.Scheduler;

internal sealed class TickRegistration : IDisposable
{
    private readonly UnrealRuntime _runtime;
    private Action? _callback;

    internal TickRegistration(UnrealRuntime runtime, Action callback)
    {
        _runtime = runtime;
        _callback = callback;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _callback, null) is { } callback)
            _runtime.RemoveTickCallback(callback);
    }
}