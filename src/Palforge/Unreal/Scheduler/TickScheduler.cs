using Microsoft.Extensions.Logging;

namespace Palforge.Unreal.Scheduler;

internal sealed class TickScheduler
{
    private readonly ILogger _log;
    private readonly Lock _gate;
    private readonly List<ScheduledWork> _work;

    private ScheduledWork[] _snapshot;
    private long _frames;

    public TickScheduler(ILogger log)
    {
        ArgumentNullException.ThrowIfNull(log);

        _log = log;
        _gate = new Lock();
        _work = [];
        _snapshot = [];
    }

    public IDisposable AfterFrames(int frames, Action callback)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        ArgumentNullException.ThrowIfNull(callback);

        return Add(new ScheduledWork(this, callback, timed: false, _frames + frames, 0));
    }

    public IDisposable EveryFrames(int frames, Action callback)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frames, 1);
        ArgumentNullException.ThrowIfNull(callback);

        return Add(new ScheduledWork(this, callback, timed: false, _frames + frames, frames));
    }

    public IDisposable After(TimeSpan delay, Action callback)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(delay.Ticks);
        ArgumentNullException.ThrowIfNull(callback);

        return Add(new ScheduledWork(this, callback, timed: true, Now + (long)delay.TotalMilliseconds, 0));
    }

    public IDisposable Every(TimeSpan interval, Action callback)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval.Ticks, 0);
        ArgumentNullException.ThrowIfNull(callback);

        var milliseconds = Math.Max(1, (long)interval.TotalMilliseconds);

        return Add(new ScheduledWork(this, callback, timed: true, Now + milliseconds, milliseconds));
    }

    internal void Remove(ScheduledWork work)
    {
        lock (_gate)
        {
            _work.Remove(work);

            _snapshot = [.. _work];
        }
    }

    public void Tick()
    {
        _frames++;

        var snapshot = _snapshot;

        if (snapshot.Length is 0)
            return;

        var now = Now;
        var completed = false;

        foreach (var work in snapshot)
        {
            if (!work.IsDue(_frames, now))
                continue;

            work.Invoke(_log);

            completed |= work.IsRevoked;
        }

        if (completed)
            Prune();
    }

    private static long Now =>
        Environment.TickCount64;

    private ScheduledWork Add(ScheduledWork work)
    {
        lock (_gate)
        {
            _work.Add(work);
            _snapshot = [.. _work];
        }

        return work;
    }

    private void Prune()
    {
        lock (_gate)
        {
            _work.RemoveAll(static work => work.IsRevoked);
            _snapshot = [.. _work];
        }
    }
}