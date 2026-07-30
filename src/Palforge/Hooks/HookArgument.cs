using Palforge.Unreal.Hooks;

namespace Palforge.Hooks;

internal sealed record HookArgument(Func<HookContext, object?> Read, Action<HookContext, object?>? Write);