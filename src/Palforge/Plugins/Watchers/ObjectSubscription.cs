using Microsoft.Extensions.Logging;
using Palforge.Unreal.Reflection;
using Palforge.Unreal.Threading;

namespace Palforge.Plugins.Watchers;

internal sealed class ObjectSubscription : IDisposable
{
    private readonly ObjectWatcher _watcher;
    private readonly string _className;
    private readonly Action<UObject> _callback;

    private bool _withdrawn;

    internal ObjectSubscription(ObjectWatcher watcher, string className, Action<UObject> callback)
    {
        _watcher = watcher;
        _className = className;
        _callback = callback;
    }

    internal void Deliver(UObject created, ILogger log)
    {
        if (_withdrawn || !Matches(created))
            return;

        GameThreadGuard.Run(() => _callback(created), log, $"A new-object callback for '{_className}'");
    }

    public void Dispose()
    {
        if (_withdrawn)
            return;

        _withdrawn = true;

        _watcher.Remove(this);
    }

    private bool Matches(UObject created)
    {
        for (var klass = created.Class; klass is not null; klass = klass.SuperClass)
        {
            if (string.Equals(klass.Name, _className, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}