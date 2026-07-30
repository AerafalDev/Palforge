using Microsoft.Extensions.Logging;
using Palforge.Unreal.Reflection;
using Palforge.Unreal.Runtime;
using Palforge.Unreal.Threading;

namespace Palforge.Plugins.Watchers;

internal sealed class WorldWatcher
{
    private const string GameStateClass = "GameStateBase";

    private readonly UnrealRuntime _runtime;
    private readonly ObjectWatcher _objects;
    private readonly ILogger _log;

    public WorldWatcher(UnrealRuntime runtime, ObjectWatcher objects, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(log);

        _runtime = runtime;
        _objects = objects;
        _log = log;
    }

    public IDisposable OnLoaded(Action<UObject> callback)
    {
        var subscription = _objects.Add(GameStateClass, gameState =>
        {
            _log.LogDebug("World loaded: game state {Name} ({Class})", gameState.Name, gameState.Class?.Name);
            callback(gameState);
        });

        if (_runtime.IsReady && _runtime.Reflection.FindFirstOf(GameStateClass) is { } current)
            GameThread.Post(() => GameThreadGuard.Run(() => callback(current), _log, "A world-loaded callback (for the world already in play)"));

        return subscription;
    }
}