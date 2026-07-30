using Microsoft.Extensions.Logging;
using Palforge.Unreal;

namespace Palforge.Plugins;

public abstract class Plugin
{
    private PluginServices? _attached;

    protected ILogger Log =>
        Attached.Log;

    protected UnrealApi Unreal =>
        Attached.Unreal;

    protected PluginScope Scope =>
        Attached.Scope;

    protected IServiceProvider Services =>
        Attached.Services;

    private PluginServices Attached =>
        _attached ?? throw new InvalidOperationException($"{GetType().Name} is not attached yet — the host attaches a plugin right after resolving it, so these are unavailable in a constructor or field initialiser. Take what you need through the constructor, or use OnStart.");

    protected virtual void OnStart()
    {
    }

    protected virtual void OnStop()
    {
    }

    internal void Attach(PluginServices services)
    {
        _attached = services;
    }

    internal void Start()
    {
        OnStart();
    }

    internal void Stop()
    {
        OnStop();
    }
}