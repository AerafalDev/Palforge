using Microsoft.Extensions.Logging;
using Palforge.Plugins.Watchers;
using Palforge.Unreal;
using Palforge.Unreal.Hooks;
using Palforge.Unreal.Reflection;
using Palforge.Unreal.Runtime;
using Palforge.Unreal.Threading;

namespace Palforge.Plugins;

public sealed class PluginScope
{
    private readonly string _plugin;
    private readonly UnrealRuntime _runtime;
    private readonly UnrealApi _unreal;
    private readonly ObjectWatcher _objects;
    private readonly WorldWatcher _worlds;
    private readonly ILogger _log;
    private readonly List<IDisposable> _registrations;
    private readonly Lock _gate;

    private bool _revoked;

    internal PluginScope(string plugin, UnrealRuntime runtime, UnrealApi unreal, ObjectWatcher objects, WorldWatcher worlds, ILogger log)
    {
        _plugin = plugin;
        _runtime = runtime;
        _unreal = unreal;
        _objects = objects;
        _worlds = worlds;
        _log = log;
        _registrations = [];
        _gate = new Lock();
    }

    public IDisposable Hook(string className, string functionName, HookCallback? prefix = null, HookCallback? postfix = null, bool includeOverrides = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(className);
        ArgumentException.ThrowIfNullOrEmpty(functionName);

        return Track(_runtime.Hooks.Register(className, functionName, prefix, postfix, includeOverrides));
    }

    public IDisposable HookByName(string functionName, HookCallback? prefix = null, HookCallback? postfix = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(functionName);

        return Track(_runtime.Hooks.RegisterByName(functionName, prefix, postfix));
    }

    public IDisposable OnNewObject<T>(Action<T> callback)
        where T : UObject
    {
        ArgumentNullException.ThrowIfNull(callback);

        var className = _unreal.EngineNameOf(typeof(T))
            ?? throw new InvalidOperationException($"{typeof(T).Name} is not a generated SDK type, so it names no engine class. Use the class-name form, or regenerate the SDK if the class is new.");

        return OnNewObject(className, created => callback((T)created));
    }

    public IDisposable OnNewObject(string className, Action<UObject> callback)
    {
        ArgumentException.ThrowIfNullOrEmpty(className);
        ArgumentNullException.ThrowIfNull(callback);

        return Track(_objects.Add(className, callback));
    }

    public IDisposable OnTick(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        return Track(_runtime.AddTickCallback(() => GameThreadGuard.Run(callback, _log, $"Plugin {_plugin}: a tick callback")));
    }

    public IDisposable OnMapLoaded(Action<UObject> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        return Track(_worlds.OnLoaded(callback));
    }

    public IDisposable AfterFrames(int frames, Action callback)
    {
        return Track(_runtime.Scheduler.AfterFrames(frames, callback));
    }

    public IDisposable EveryFrames(int frames, Action callback)
    {
        return Track(_runtime.Scheduler.EveryFrames(frames, callback));
    }

    public IDisposable After(TimeSpan delay, Action callback)
    {
        return Track(_runtime.Scheduler.After(delay, callback));
    }

    public IDisposable Every(TimeSpan interval, Action callback)
    {
        return Track(_runtime.Scheduler.Every(interval, callback));
    }

    public IDisposable Track(IDisposable registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (_gate)
        {
            if (!_revoked)
            {
                _registrations.Add(registration);

                return registration;
            }
        }

        registration.Dispose();

        throw new ObjectDisposedException(nameof(PluginScope), "The plugin has stopped; its registrations are no longer accepted.");
    }

    internal void Revoke()
    {
        IDisposable[] registrations;

        lock (_gate)
        {
            if (_revoked)
                return;

            _revoked = true;
            registrations = [.. _registrations];
            _registrations.Clear();
        }

        for (var index = registrations.Length - 1; index >= 0; index--)
        {
            try
            {
                registrations[index].Dispose();
            }
            catch (Exception exception)
            {
                _log.LogError(exception, "Plugin {Plugin}: revoking a registration threw", _plugin);
            }
        }
    }
}