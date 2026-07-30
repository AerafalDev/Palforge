using System.Reflection;
using Microsoft.Extensions.Logging;
using Palforge.Hooks.Attributes;
using Palforge.Plugins;
using Palforge.Unreal;

namespace Palforge.Hooks;

internal static class HookApi
{
    private const BindingFlags Candidates = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    public static int Attach(Assembly assembly, IServiceProvider services, UnrealApi unreal, PluginScope scope, ILogger log)
    {
        var attached = 0;

        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(Candidates))
            {
                foreach (var attribute in method.GetCustomAttributes<HookAttribute>(inherit: false))
                {
                    var target = Attach(method, attribute, services, unreal, scope);
                    log.LogDebug("Hook {Method} attached to {Class}.{Function}", $"{type.Name}.{method.Name}", ClassNameOf(attribute, unreal), target);
                    attached++;
                }
            }
        }

        return attached;
    }

    private static string Attach(MethodInfo method, HookAttribute attribute, IServiceProvider services, UnrealApi unreal, PluginScope scope)
    {
        var className = ClassNameOf(attribute, unreal);
        var function = HookBinder.Resolve(method, className, attribute.FunctionName, unreal);
        var prefix = attribute is PreHookAttribute;
        var callback = HookBinder.Bind(method, method.IsStatic ? null : Instance(method, services), function, prefix);

        scope.Hook(className, function.Name, prefix ? callback : null, prefix ? null : callback, attribute.IncludeOverrides);

        return function.Name;
    }

    private static string ClassNameOf(HookAttribute attribute, UnrealApi unreal)
    {
        if (attribute.ClassName is { } named)
            return named;

        var wrapper = attribute.Wrapper!;

        return unreal.EngineNameOf(wrapper) ?? throw new InvalidOperationException($"{wrapper.Name} is not a generated SDK type, so it names no engine class — hook by class name instead, or regenerate the SDK.");
    }

    private static object Instance(MethodInfo method, IServiceProvider services)
    {
        var type = method.DeclaringType!;

        return services.GetService(type)
            ?? Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"{type.Name} declares a hook but could not be created — register it in ConfigureServices, or make the hook method static.");
    }
}