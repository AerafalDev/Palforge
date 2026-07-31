using Microsoft.Extensions.DependencyInjection;
using Palforge.Commands.Modules;
using Palforge.Commands.Permissions;
using Palforge.Commands.Preconditions;

namespace Palforge.Commands.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class CommandPermissionsAttribute : CommandPreconditionAttribute
{
    public IReadOnlyList<string> Permissions { get; }

    public CommandPermissionsAttribute(params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        Permissions = permissions;
    }

    public override PreconditionResult Check(CommandContext context, IServiceProvider services)
    {
        var provider = services.GetService<PermissionProvider>() ?? AdminPermissionProvider.Instance;

        foreach (var permission in Permissions)
        {
            if (!provider.HasPermission(context, permission))
                return PreconditionResult.Fail($"You do not have permission to use this ({permission}).");
        }

        return PreconditionResult.Success;
    }
}