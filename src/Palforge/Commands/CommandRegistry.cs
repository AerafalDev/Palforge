using Palforge.Commands.Arguments;

namespace Palforge.Commands;

internal sealed class CommandRegistry
{
    private readonly Dictionary<string, CommandInfo> _top;
    private readonly Dictionary<string, Dictionary<string, CommandInfo>> _groups;
    private readonly Lock _gate;

    public CommandRegistry()
    {
        _top = new Dictionary<string, CommandInfo>(StringComparer.OrdinalIgnoreCase);
        _groups = new Dictionary<string, Dictionary<string, CommandInfo>>(StringComparer.OrdinalIgnoreCase);
        _gate = new Lock();
    }

    public IDisposable Add(CommandInfo command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var keys = new List<Key>();

        lock (_gate)
        {
            if (command.GroupName is { } groupName)
            {
                foreach (var group in Names(groupName, command.GroupAliases))
                {
                    if (!_groups.TryGetValue(group, out var commands))
                        _groups[group] = commands = new Dictionary<string, CommandInfo>(StringComparer.OrdinalIgnoreCase);

                    foreach (var name in Names(command.Name, command.Aliases))
                    {
                        commands[name] = command;
                        keys.Add(new Key(group, name));
                    }
                }
            }
            else
            {
                foreach (var name in Names(command.Name, command.Aliases))
                {
                    _top[name] = command;
                    keys.Add(new Key(null, name));
                }
            }
        }

        return new Registration(this, command, keys);
    }

    public bool TryResolve(ReadOnlySpan<char> input, out CommandInfo command, out ReadOnlySpan<char> arguments)
    {
        command = null!;
        arguments = default;

        var reader = new RawArgumentReader(input);

        if (!reader.TryReadToken(out var first))
            return false;

        var head = first.ToString();

        lock (_gate)
        {
            if (_groups.TryGetValue(head, out var commands))
            {
                if (reader.TryReadToken(out var second) && commands.TryGetValue(second.ToString(), out var grouped))
                {
                    command = grouped;
                    arguments = reader.Remaining;
                    return true;
                }

                return false;
            }

            if (_top.TryGetValue(head, out var top))
            {
                command = top;
                arguments = reader.Remaining;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> Names(string name, IReadOnlyList<string> aliases)
    {
        yield return name;

        foreach (var alias in aliases)
            yield return alias;
    }

    private void Remove(CommandInfo command, List<Key> keys)
    {
        lock (_gate)
        {
            foreach (var key in keys)
            {
                if (key.Group is null)
                {
                    if (_top.TryGetValue(key.Name, out var current) && ReferenceEquals(current, command))
                        _top.Remove(key.Name);
                }
                else if (_groups.TryGetValue(key.Group, out var commands))
                {
                    if (commands.TryGetValue(key.Name, out var current) && ReferenceEquals(current, command))
                        commands.Remove(key.Name);

                    if (commands.Count is 0)
                        _groups.Remove(key.Group);
                }
            }
        }
    }

    private readonly record struct Key(string? Group, string Name);

    private sealed class Registration : IDisposable
    {
        private readonly CommandRegistry _registry;
        private readonly CommandInfo _command;
        private readonly List<Key> _keys;

        private bool _removed;

        public Registration(CommandRegistry registry, CommandInfo command, List<Key> keys)
        {
            _registry = registry;
            _command = command;
            _keys = keys;
        }

        public void Dispose()
        {
            if (_removed)
                return;

            _removed = true;
            _registry.Remove(_command, _keys);
        }
    }
}