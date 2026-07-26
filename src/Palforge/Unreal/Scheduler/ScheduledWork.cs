using Microsoft.Extensions.Logging;
using Palforge.Unreal.Threading;

namespace Palforge.Unreal.Scheduler;

internal sealed class ScheduledWork : IDisposable
{
    private readonly TickScheduler _scheduler;
    private readonly Action _callback;
    private readonly bool _timed;
    private readonly long _interval;

    private long _due;
    private bool _revoked;

    internal bool IsRevoked =>
        _revoked;

    internal ScheduledWork(TickScheduler scheduler, Action callback, bool timed, long due, long interval)
    {
        _scheduler = scheduler;
        _callback = callback;
        _timed = timed;
        _due = due;
        _interval = interval;
    }

    internal bool IsDue(long frames, long milliseconds)
    {
        if (_revoked)
            return false;

        var now = _timed ? milliseconds : frames;

        if (now < _due)
            return false;

        if (_interval > 0)
            _due += _interval;
        else
            _revoked = true;

        return true;
    }

    internal void Invoke(ILogger log)
    {
        GameThreadGuard.Run(_callback, log, "Scheduled work");
    }

    public void Dispose()
    {
        if (_revoked)
            return;

        _revoked = true;

        _scheduler.Remove(this);
    }
}