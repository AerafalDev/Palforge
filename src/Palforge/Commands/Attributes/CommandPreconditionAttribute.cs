using Palforge.Commands.Modules;
using Palforge.Commands.Preconditions;

namespace Palforge.Commands.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public abstract class CommandPreconditionAttribute : Attribute
{
    public abstract PreconditionResult Check(CommandContext context, IServiceProvider services);
}