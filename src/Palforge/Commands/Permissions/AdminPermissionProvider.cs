using Palforge.Commands.Modules;

namespace Palforge.Commands.Permissions;

internal sealed class AdminPermissionProvider : PermissionProvider
{
    public static readonly AdminPermissionProvider Instance = new AdminPermissionProvider();

    public override bool HasPermission(CommandContext context, string permission)
    {
        return context.IsAdministrator;
    }
}