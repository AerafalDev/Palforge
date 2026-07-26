namespace Palforge.Unreal.Hooks;

internal sealed class HookSubscription : IDisposable
{
    private readonly HookManager _manager;
    private readonly HookEntry _entry;
    private bool _disposed;

    public HookSubscription(HookManager manager, HookEntry entry)
    {
        _manager = manager;
        _entry = entry;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _manager.Remove(_entry);
    }
}