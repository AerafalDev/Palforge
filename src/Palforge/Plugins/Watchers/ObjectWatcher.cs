using Microsoft.Extensions.Logging;
using Palforge.Unreal.Reflection;
using Palforge.Unreal.Runtime;

namespace Palforge.Plugins.Watchers;

internal sealed class ObjectWatcher
{
    private const int MaxPerTick = 4096;

    private readonly UnrealRuntime _runtime;
    private readonly ILogger _log;
    private readonly List<ObjectSubscription> _subscriptions;
    private readonly Lock _gate;

    private int _seen;

    internal ObjectWatcher(UnrealRuntime runtime, ILogger log)
    {
        _runtime = runtime;
        _log = log;
        _subscriptions = [];
        _gate = new Lock();
    }

    internal void Prime()
    {
        _seen = _runtime.IsReady
            ? _runtime.Reflection.ObjectCount
            : 0;
    }

    internal IDisposable Add(string className, Action<UObject> callback)
    {
        var subscription = new ObjectSubscription(this, className, callback);

        lock (_gate)
            _subscriptions.Add(subscription);

        return subscription;
    }

    internal void Remove(ObjectSubscription subscription)
    {
        lock (_gate)
            _subscriptions.Remove(subscription);
    }

    internal void OnGameThreadTick()
    {
        ObjectSubscription[] subscriptions;

        lock (_gate)
        {
            if (_subscriptions.Count is 0)
            {
                _seen = _runtime.IsReady ? _runtime.Reflection.ObjectCount : _seen;

                return;
            }

            subscriptions = [.. _subscriptions];
        }

        if (!_runtime.IsReady)
            return;

        var context = _runtime.Reflection;
        var count = context.ObjectCount;

        if (count <= _seen)
        {
            _seen = count;
            return;
        }

        var examined = 0;

        foreach (var created in context.ObjectsFrom(_seen))
        {
            if (!created.IsTemplate)
            {
                foreach (var subscription in subscriptions)
                    subscription.Deliver(created, _log);
            }

            if (++examined >= MaxPerTick)
                break;
        }

        _seen += examined;
    }
}