using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Palforge.Commands;
using Palforge.Commands.Arguments;
using Palforge.Commands.Attributes;
using Palforge.Commands.Options;
using Palforge.Extensions;
using Palforge.Hooks;
using Palforge.Hosting.Options;
using Palforge.Plugins.Watchers;
using Palforge.Unreal;
using Palforge.Unreal.Runtime;
using Serilog;

namespace Palforge.Plugins;

internal sealed class PluginApi : IDisposable
{
    private const int WaitForDebuggerSeconds = 300;

    private readonly ILogger<PluginApi> _log;
    private readonly ILoggerFactory _loggers;
    private readonly List<PluginLoaded> _plugins;
    private readonly DebugOptions _debugOptions;
    private readonly CommandOptions _commandOptions;

    private UnrealRuntime? _runtime;
    private ObjectWatcher? _objects;
    private WorldWatcher? _worlds;

    private CommandRegistry? _commands;
    private IDisposable? _commandHook;

    public PluginApi(ILogger<PluginApi> log, ILoggerFactory loggers, DebugOptions debugOptions, CommandOptions commandOptions)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(loggers);
        ArgumentNullException.ThrowIfNull(debugOptions);
        ArgumentNullException.ThrowIfNull(commandOptions);
        _log = log;
        _loggers = loggers;
        _debugOptions = debugOptions;
        _commandOptions = commandOptions;
        _plugins = [];
    }

    public void StartAll(UnrealRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        _runtime = runtime;
        _objects = new ObjectWatcher(runtime, _loggers.CreateLogger(nameof(ObjectWatcher)));
        _worlds = new WorldWatcher(runtime, _objects, _loggers.CreateLogger(nameof(WorldWatcher)));

        _objects.Prime();

        runtime.AddTickCallback(_objects.OnGameThreadTick);

        _commands = new CommandRegistry();
        var commandService = new CommandService(_commands, _commandOptions, new UnrealApi(runtime), _loggers.CreateLogger(nameof(CommandService)));
        _commandHook = commandService.Install(runtime);

        RegisterHostCommands();

        var directory = Path.PalforgePluginsDirectory;

        if (!Directory.Exists(directory))
        {
            _log.LogInformation("Plugins: none loaded ('{Directory}' does not exist)", directory);
            return;
        }

        WaitForDebugger();

        var candidates = Directory.GetDirectories(directory);

        foreach (var candidate in candidates)
            LoadDirectory(candidate);

        _log.LogInformation("Plugins: {Loaded}/{Found} started from '{Directory}'", _plugins.Count, candidates.Length, directory);
    }

    public void StopAll()
    {
        foreach (var plugin in _plugins.ToArray())
        {
            Teardown(plugin);

            _log.LogInformation("Plugin {Plugin}: stopped", plugin.Name);
        }

        _commandHook?.Dispose();
        _commandHook = null;
    }

    public IReadOnlyList<PluginMetadata> List()
    {
        return [.. _plugins.Select(static plugin => plugin.Metadata)];
    }

    public bool Load(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_runtime is null)
            return false;

        if (Find(name) is not null)
        {
            _log.LogWarning("Plugin {Plugin}: already loaded", name);

            return false;
        }

        var directory = Path.Combine(Path.PalforgePluginsDirectory, name);

        if (!Directory.Exists(directory))
        {
            _log.LogWarning("Plugin {Plugin}: no directory '{Directory}'", name, directory);

            return false;
        }

        var before = _plugins.Count;

        LoadDirectory(directory);

        return _plugins.Count > before;
    }

    public bool Unload(string idOrName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrName);

        var pending = TeardownForUnload(idOrName);

        if (pending is not ({ } name, { } weak))
            return false;

        _runtime?.Scheduler.AfterFrames(2, () =>
        {
            if (Collected(weak))
                _log.LogInformation("Plugin {Plugin}: unloaded and collected", name);
            else
                _log.LogWarning("Plugin {Plugin}: unloaded, but its load context was not collected — something still holds a reference to it (often a thread or task the plugin started and did not stop in OnStop)", name);
        });

        return true;
    }

    public bool Reload(string idOrName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrName);

        if (Find(idOrName) is not { } plugin)
            return false;

        var name = plugin.Name;

        Unload(plugin.Metadata.Id);

        return Load(name);
    }

    public void Dispose()
    {
        StopAll();
    }

    private PluginLoaded? Find(string idOrName)
    {
        return _plugins.FirstOrDefault(plugin =>
            string.Equals(plugin.Metadata.Id, idOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(plugin.Name, idOrName, StringComparison.OrdinalIgnoreCase));
    }

    private void Teardown(PluginLoaded plugin)
    {
        Guard(plugin.Name, "stopping", plugin.Instance.Stop);
        Guard(plugin.Name, "revoking registrations", plugin.Scope.Revoke);

        if (plugin.Instance is IDisposable disposable)
            Guard(plugin.Name, "disposing the plugin", disposable.Dispose);

        Guard(plugin.Name, "disposing services", plugin.Services.Dispose);

        if (plugin.Configuration is IDisposable configuration)
            Guard(plugin.Name, "disposing configuration", configuration.Dispose);

        _plugins.Remove(plugin);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private (string Name, WeakReference Context)? TeardownForUnload(string idOrName)
    {
        if (Find(idOrName) is not { } plugin)
            return null;

        var name = plugin.Name;
        var context = plugin.Context;

        Teardown(plugin);
        context.Unload();

        return (name, new WeakReference(context, trackResurrection: true));
    }

    private static bool Collected(WeakReference context)
    {
        for (var attempt = 0; attempt < 10 && context.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return !context.IsAlive;
    }

    private void LoadDirectory(string directory)
    {
        var name = Path.GetFileName(directory);
        var assemblyPath = Path.Combine(directory, name + ".dll");

        if (!File.Exists(assemblyPath))
        {
            _log.LogWarning("Plugin {Plugin}: skipped, '{Path}' was not found (a plugin directory holds an assembly named after it)", name, assemblyPath);
            return;
        }

        var context = new PluginLoadContext(name, assemblyPath);

        try
        {
            var assembly = context.LoadFromAssemblyPath(assemblyPath);

            if (FindType<Plugin>(assembly) is not { } type)
            {
                _log.LogWarning("Plugin {Plugin}: skipped, no public class deriving from {Base}", name, nameof(Plugin));
                return;
            }

            var metadata = PluginMetadata.For(name, type, assembly);

            _plugins.Add(Activate(name, metadata, type, FindType<PluginConfiguration>(assembly), directory, context, assembly));

            _log.LogInformation("Plugin {Plugin}: started — {Metadata}", name, metadata);
        }
        catch (Exception exception)
        {
            _log.LogError(exception, "Plugin {Plugin}: failed to start", name);
        }
    }

    private PluginLoaded Activate(string name, PluginMetadata metadata, Type type, Type? configurationType, string directory, PluginLoadContext context, Assembly assembly)
    {
        var runtime = _runtime ?? throw new InvalidOperationException("plugins cannot be activated before the runtime is handed to StartAll");
        var objects = _objects!;
        var worlds = _worlds!;
        var services = new ServiceCollection();

        var api = new UnrealApi(runtime);
        var configuration = ReadConfiguration(name, directory);

        var loggers = LoggerFactory.Create(static builder => builder.AddSerilog(dispose: false));
        var scope = new PluginScope(name, runtime, api, objects, worlds, loggers.CreateLogger(name));

        services.AddSingleton(loggers);
        services.AddLogging();
        services.AddSingleton(api);
        services.AddSingleton(scope);
        services.AddSingleton(configuration);
        services.AddSingleton(type);

        if (configurationType is not null)
            ((PluginConfiguration)Activator.CreateInstance(configurationType)!).Configure(services, configuration);

        var provider = services.BuildServiceProvider();
        var instance = (Plugin)provider.GetRequiredService(type);

        instance.Attach(new PluginServices(loggers.CreateLogger(type.FullName ?? name), api, scope, provider));

        var hooks = HookApi.Attach(assembly, provider, api, scope, loggers.CreateLogger(name));

        if (hooks > 0)
            _log.LogInformation("Plugin {Plugin}: {Hooks} hook(s) attached", name, hooks);

        var commands = PluginCommandBinder.Attach(assembly, provider, api, scope, _commands!, metadata.Id, loggers.CreateLogger(name));

        if (commands > 0)
            _log.LogInformation("Plugin {Plugin}: {Commands} command(s) registered", name, commands);

        instance.Start();

        return new PluginLoaded(name, metadata, instance, scope, provider, configuration, context, directory);
    }

    private IConfiguration ReadConfiguration(string name, string directory)
    {
        var file = Path.Combine(directory, name + ".cfg");

        if (!File.Exists(file))
            _log.LogDebug("Plugin {Plugin}: no '{File}', starting with an empty configuration", name, Path.GetFileName(file));

        return new ConfigurationBuilder()
            .AddIniFile(file, optional: true, reloadOnChange: true)
            .Build();
    }

    private void RegisterHostCommands()
    {
        var provider = new ServiceCollection()
            .AddSingleton(_loggers)
            .AddLogging()
            .AddSingleton(this)
            .BuildServiceProvider();

        var readers = new ArgumentReaderRegistry();
        var group = typeof(PluginCommands).GetCustomAttribute<CommandGroupAttribute>()?.Name;
        var aliases = typeof(PluginCommands).GetCustomAttribute<CommandAliasesAttribute>()?.Aliases ?? [];

        foreach (var method in typeof(PluginCommands).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (CommandBinder.IsCommand(method))
                _commands!.Add(CommandBinder.Build(typeof(PluginCommands), method, "palforge", provider, group, aliases, readers));
        }
    }

    private void WaitForDebugger()
    {
        if (!_debugOptions.EnableDebugger || Debugger.IsAttached)
            return;

        _log.LogWarning("Wait for debugger is set — pausing for a debugger to attach to process {ProcessId} (the server is frozen until you attach, or {Timeout}s pass)", Environment.ProcessId, WaitForDebuggerSeconds);

        var deadline = Environment.TickCount64 + WaitForDebuggerSeconds * 1000L;

        while (!Debugger.IsAttached && Environment.TickCount64 < deadline)
            Thread.Sleep(200);

        if (Debugger.IsAttached)
            _log.LogInformation("Debugger attached — continuing");
        else
            _log.LogWarning("No debugger attached within {Timeout}s — continuing", WaitForDebuggerSeconds);
    }

    private static Type? FindType<T>(Assembly assembly)
    {
        foreach (var type in assembly.GetExportedTypes())
        {
            if (!type.IsAbstract && typeof(T).IsAssignableFrom(type))
                return type;
        }

        return null;
    }

    private void Guard(string name, string stage, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            _log.LogError(exception, "Plugin {Plugin}: {Stage} threw", name, stage);
        }
    }
}