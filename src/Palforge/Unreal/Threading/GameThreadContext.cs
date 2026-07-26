namespace Palforge.Unreal.Threading;

internal sealed class GameThreadContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        GameThread.Schedule(() => callback(state));
    }

    public override void Send(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (GameThread.IsCurrent)
        {
            callback(state);
            return;
        }

        Post(callback, state);
    }

    public override SynchronizationContext CreateCopy()
    {
        return this;
    }
}