using Microsoft.Extensions.Logging;
using Palforge.Commands.Modules.Chat;
using Palforge.Commands.Options;
using Palforge.Sdk.Script.Pal;
using Palforge.Unreal;
using Palforge.Unreal.Hooks;
using Palforge.Unreal.Reflection;
using Palforge.Unreal.Runtime;

namespace Palforge.Commands;

internal sealed class CommandService
{
    private const string ChatClass = "PalPlayerController";
    private const string ChatFunction = "EnterChat_Receive";

    private readonly CommandRegistry _registry;
    private readonly CommandOptions _options;
    private readonly UnrealApi _unreal;
    private readonly ILogger _log;

    public CommandService(CommandRegistry registry, CommandOptions options, UnrealApi unreal, ILogger log)
    {
        _registry = registry;
        _options = options;
        _unreal = unreal;
        _log = log;
    }

    public IDisposable Install(UnrealRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        return runtime.Hooks.Register(ChatClass, ChatFunction, prefix: OnChat, includeOverrides: true);
    }

    private void OnChat(HookContext context)
    {
        try
        {
            if (context.Self is not PalPlayerController player)
                return;

            var message = Message(context);

            if (!TryStripPrefix(message, out var body))
                return;

            if (Dispatch(new ChatCommandContext(player, message, _unreal, _options.SenderName), body))
                context.SkipOriginal();
        }
        catch (Exception exception)
        {
            _log.LogError(exception, "Command dispatch threw handling a chat message");
        }
    }

    private bool Dispatch(ChatCommandContext context, ReadOnlySpan<char> body)
    {
        if (!_registry.TryResolve(body, out var command, out var arguments))
        {
            context.Reply("Unknown command.");
            return true;
        }

        var precondition = command.CheckPreconditions(context);

        if (!precondition.IsSuccess)
        {
            context.Reply(precondition.ErrorMessage!);
            return true;
        }

        if (!command.TryReadArguments(context, arguments, out var values, out var error))
        {
            context.Reply(error!);
            return true;
        }

        try
        {
            command.Invoke(context, values);
        }
        catch (Exception exception)
        {
            _log.LogError(exception, "Command '{Command}' threw", command.Name);
            context.Reply("The command failed.");
        }

        return true;
    }

    private bool TryStripPrefix(string message, out ReadOnlySpan<char> body)
    {
        var span = message.AsSpan().TrimStart();
        var prefix = _options.Prefix.AsSpan();

        if (prefix.Length is not 0 && span.StartsWith(prefix, StringComparison.Ordinal))
        {
            body = span[prefix.Length..].TrimStart();
            return !body.IsEmpty;
        }

        body = default;
        return false;
    }

    private static string Message(HookContext context)
    {
        foreach (var parameter in context.Function.Parameters)
        {
            if (parameter is FStrProperty)
                return context.GetString(parameter.Name);
        }

        return string.Empty;
    }
}