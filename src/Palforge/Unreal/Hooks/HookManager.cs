using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Hooks;

internal sealed class HookManager
{
    private const int RescanIntervalTicks = 600;

    private readonly UnrealContext _context;
    private readonly ILogger _log;
    private readonly ProcessEventInterceptor _interceptor;
    private readonly ConcurrentDictionary<int, HookEntry[]> _byName;
    private readonly ConcurrentDictionary<int, byte> _deferredNames;
    private readonly ConcurrentDictionary<HookCallback, byte> _reported;
    private readonly Lock _gate;

    private int _tick;
    private int _scanned;

    internal HookManager(UnrealContext context, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(log);

        _byName = [];
        _deferredNames = [];
        _reported = [];
        _gate = new Lock();
        _context = context;
        _log = log;
        _interceptor = new ProcessEventInterceptor(context.Memory, context.ProcessEventByteOffset, Before, After);
    }

    public IDisposable RegisterByName(string functionName, HookCallback? prefix = null, HookCallback? postfix = null)
    {
        return Register(0, functionName, prefix, postfix);
    }

    public IDisposable Register(string className, string functionName, HookCallback? prefix = null, HookCallback? postfix = null, bool includeOverrides = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(className);

        var filter = _context.FindClass(className)?.Address
            ?? throw new ArgumentException($"no class named '{className}' exists in the running game", nameof(className));

        return Register(filter, functionName, prefix, postfix, includeOverrides);
    }

    internal void Remove(HookEntry entry)
    {
        lock (_gate)
        {
            if (!_byName.TryGetValue(entry.NameId, out var entries))
                return;

            var remaining = entries.Where(candidate => candidate != entry).ToArray();

            if (remaining.Length is 0)
                _byName.TryRemove(entry.NameId, out _);
            else
                _byName[entry.NameId] = remaining;

            if (!remaining.Any(static candidate => candidate.Wide))
                _deferredNames.TryRemove(entry.NameId, out _);
        }

        if (entry.Prefix is { } prefix)
            _reported.TryRemove(prefix, out _);

        if (entry.Postfix is { } postfix)
            _reported.TryRemove(postfix, out _);
    }

    internal void OnGameThreadTick()
    {
        if (_deferredNames.IsEmpty || Interlocked.Increment(ref _tick) % RescanIntervalTicks is not 0)
            return;

        var count = _context.ObjectCount;

        if (count <= _scanned)
        {
            _scanned = count;

            return;
        }

        foreach (var vtable in _context.ClassVtablesWithAnyFunction(_deferredNames.Keys.ToHashSet(), _scanned))
            _interceptor.InstallOn(vtable);

        _scanned = count;
    }

    private HookSubscription Register(nint classFilter, string functionName, HookCallback? prefix, HookCallback? postfix, bool includeOverrides = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(functionName);

        if (prefix is null && postfix is null)
            throw new ArgumentException("a hook needs a prefix or a postfix", nameof(prefix));

        if (!_context.TryFindNameId(functionName, out var nameId) || nameId is 0)
            throw new ArgumentException($"no function named '{functionName}' is known to the name pool", nameof(functionName));

        var entry = new HookEntry(nameId, classFilter, classFilter is 0 || includeOverrides, prefix, postfix);

        lock (_gate)
        {
            var existing = _byName.TryGetValue(nameId, out var entries) ? entries : [];
            _byName[nameId] = [.. existing, entry];
        }

        InstallVtables(nameId, classFilter, includeOverrides);

        return new HookSubscription(this, entry);
    }

    private void InstallVtables(int nameId, nint classFilter, bool includeOverrides)
    {
        if (classFilter is not 0 && !includeOverrides)
        {
            _interceptor.InstallOn(_context.ClassVtable(classFilter));
            return;
        }

        _deferredNames.TryAdd(nameId, 0);

        foreach (var vtable in _context.ClassVtablesWithFunction(nameId))
            _interceptor.InstallOn(vtable);

        _scanned = _context.ObjectCount;
    }

    private HookContext? Before(nint self, nint function, nint parms)
    {
        if (!_byName.TryGetValue(_context.NameIdOf(function), out var entries))
            return null;

        var classPointer = _context.ClassPointerOf(self);
        var matched = entries.Where(entry => entry.ClassFilter is 0 || _context.IsClassOrSubclass(classPointer, entry.ClassFilter)).ToArray();

        if (matched.Length is 0)
            return null;

        var context = new HookContext(_context, self, function, parms) { Handlers = matched };

        foreach (var entry in matched)
            Invoke(entry.Prefix, context);

        return context;
    }

    private void After(HookContext context)
    {
        foreach (var entry in context.Handlers ?? [])
            Invoke(entry.Postfix, context);
    }

    private void Invoke(HookCallback? callback, HookContext context)
    {
        if (callback is null)
            return;

        try
        {
            callback(context);
        }
        catch (Exception exception)
        {
            if (_reported.TryAdd(callback, 0))
                _log.LogError(exception, "A hook callback on {Function} threw. It is reported once; the game was not affected.", context.Function.Name);
        }
    }
}