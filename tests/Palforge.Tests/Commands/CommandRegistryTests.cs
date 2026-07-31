using Microsoft.Extensions.DependencyInjection;
using Palforge.Commands;

namespace Palforge.Tests.Commands;

public sealed class CommandRegistryTests
{
    [Fact]
    public void AResolvesATopLevelCommand()
    {
        var registry = new CommandRegistry();
        registry.Add(Command("ping"));

        Assert.True(registry.TryResolve("ping 1 2".AsSpan(), out var command, out var arguments));
        Assert.Equal("ping", command.Name);
        Assert.Equal("1 2", arguments.ToString());
    }

    [Fact]
    public void BResolvesAGroupedCommand()
    {
        var registry = new CommandRegistry();
        registry.Add(Command("give", group: "economy"));

        Assert.True(registry.TryResolve("economy give Aerafal 5".AsSpan(), out var command, out var arguments));
        Assert.Equal("give", command.Name);
        Assert.Equal("Aerafal 5", arguments.ToString());
    }

    [Fact]
    public void CMatchesAnAlias()
    {
        var registry = new CommandRegistry();
        registry.Add(Command("give", aliases: ["g"]));

        Assert.True(registry.TryResolve("g Aerafal".AsSpan(), out var command, out _));
        Assert.Equal("give", command.Name);
    }

    [Fact]
    public void DIsCaseInsensitive()
    {
        var registry = new CommandRegistry();
        registry.Add(Command("Ping"));

        Assert.True(registry.TryResolve("PING".AsSpan(), out _, out _));
    }

    [Fact]
    public void EDoesNotResolveAnUnknownCommand()
    {
        var registry = new CommandRegistry();
        registry.Add(Command("ping"));

        Assert.False(registry.TryResolve("pong".AsSpan(), out _, out _));
    }

    [Fact]
    public void FDisposingTheHandleRemovesTheCommand()
    {
        var registry = new CommandRegistry();
        var handle = registry.Add(Command("ping"));

        handle.Dispose();

        Assert.False(registry.TryResolve("ping".AsSpan(), out _, out _));
    }

    private static CommandInfo Command(string name, string? group = null, IReadOnlyList<string>? aliases = null)
    {
        return new CommandInfo((_, _) => new object(), (_, _) => { })
        {
            Name = name,
            GroupName = group,
            Aliases = aliases ?? [],
            PluginId = "test",
            Services = new ServiceCollection().BuildServiceProvider(),
        };
    }
}