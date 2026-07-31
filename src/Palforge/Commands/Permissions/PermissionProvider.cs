using Palforge.Commands.Modules;

namespace Palforge.Commands.Permissions;

public abstract class PermissionProvider
{
    public abstract bool HasPermission(CommandContext context, string permission);
}