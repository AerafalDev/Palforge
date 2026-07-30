using System.Reflection;
using Palforge.Unreal;
using Palforge.Unreal.Hooks;
using Palforge.Unreal.Reflection;
using Palforge.Unreal.Sdk;

namespace Palforge.Hooks;

internal static class HookBinder
{
    private const string ResultParameter = "result";

    private static readonly MethodInfo s_valueReader = Accessor(nameof(ValueReader));
    private static readonly MethodInfo s_valueWriter = Accessor(nameof(ValueWriter));
    private static readonly MethodInfo s_returnReader = Accessor(nameof(ReturnReader));
    private static readonly MethodInfo s_returnWriter = Accessor(nameof(ReturnWriter));

    public static HookCallback Bind(MethodInfo method, object? target, UFunction function, bool prefix)
    {
        var parameters = method.GetParameters();
        var arguments = new HookArgument[parameters.Length];
        var instance = -1;

        for (var index = 0; index < parameters.Length; index++)
        {
            if (index is 0 && !parameters[index].ParameterType.IsByRef && typeof(UObject).IsAssignableFrom(parameters[index].ParameterType))
            {
                instance = index;
                arguments[index] = new HookArgument(static context => context.Self, null);

                continue;
            }

            arguments[index] = Argument(method, parameters[index], function);
        }

        var votes = prefix && method.ReturnType == typeof(bool);
        var instanceType = instance >= 0 ? parameters[instance].ParameterType : null;

        return context =>
        {
            var values = new object?[arguments.Length];

            for (var index = 0; index < arguments.Length; index++)
                values[index] = arguments[index].Read(context);

            if (instanceType is not null && !instanceType.IsInstanceOfType(values[instance]))
                return;

            var returned = method.Invoke(target, values);

            for (var index = 0; index < arguments.Length; index++)
                arguments[index].Write?.Invoke(context, values[index]);

            if (votes && returned is false)
                context.SkipOriginal();
        };
    }

    public static UFunction Resolve(MethodInfo method, string className, string functionName, UnrealApi unreal)
    {
        var owner = unreal.FindClass(className)
            ?? throw new InvalidOperationException($"{Describe(method)} hooks '{className}.{functionName}', but no class named '{className}' exists in the running game");

        return Match(owner, functionName)
            ?? throw new InvalidOperationException($"{Describe(method)} hooks '{className}.{functionName}', but '{className}' has no function named '{functionName}'");
    }

    private static UFunction? Match(UClass owner, string name)
    {
        if (owner.FindFunction(name) is { } exact)
            return exact;

        for (var klass = owner; klass is not null; klass = klass.SuperClass)
        {
            foreach (var function in klass.Functions)
            {
                if (string.Equals(SdkNaming.Identifier(function.Name), name, StringComparison.Ordinal))
                    return function;
            }
        }

        return null;
    }

    private static HookArgument Argument(MethodInfo method, ParameterInfo parameter, UFunction function)
    {
        var name = parameter.Name ?? string.Empty;
        var type = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
        var writable = parameter.ParameterType.IsByRef;

        if (string.Equals(name, ResultParameter, StringComparison.OrdinalIgnoreCase))
            return Result(method, function, type, writable);

        if (Match(function, name) is not { } matched)
            throw new InvalidOperationException($"{Describe(method)} declares a parameter '{name}', but '{function.Name}' has no parameter of that name (its parameters are: {Parameters(function)})");

        var engineName = matched.Name;

        if (typeof(UObject).IsAssignableFrom(type))
            return new HookArgument(context => context.GetObject(engineName), null);

        if (type == typeof(string))
            return new HookArgument(context => context.GetString(engineName), writable ? (context, value) => context.SetString(engineName, (string)value!) : null);

        if (typeof(UStructValue).IsAssignableFrom(type))
            return StructArgument(method, type, engineName);

        if (!IsValue(type))
            throw new InvalidOperationException($"{Describe(method)} declares a parameter '{name}' of type {type.Name}, which a hook cannot read — use a number, bool, enum, string, an object type or a generated struct");

        return new HookArgument(Reader(type, engineName), writable ? Writer(type, engineName) : null);
    }

    private static HookArgument StructArgument(MethodInfo method, Type type, string engineName)
    {
        var constructor = type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(nint), typeof(UnrealContext)], null)
            ?? throw new InvalidOperationException($"{Describe(method)} declares a struct parameter of type {type.Name}, which does not look like a generated SDK struct");

        return new HookArgument(context => context.StructAddress(engineName) is var address && address is not 0 ? constructor.Invoke([address, context.Reflection]) : null, null);
    }

    private static HookArgument Result(MethodInfo method, UFunction function, Type type, bool writable)
    {
        if (function.ReturnParameter is null)
            throw new InvalidOperationException($"{Describe(method)} declares '{ResultParameter}', but '{function.Name}' returns nothing");

        if (!IsValue(type))
            throw new InvalidOperationException($"{Describe(method)} declares '{ResultParameter}' as {type.Name}, which a hook cannot read — a return value must be a number, bool or enum");

        return new HookArgument(
            (Func<HookContext, object?>)s_returnReader.MakeGenericMethod(type).Invoke(null, null)!,
            writable ? (Action<HookContext, object?>)s_returnWriter.MakeGenericMethod(type).Invoke(null, null)! : null);
    }

    private static Func<HookContext, object?> Reader(Type type, string name)
    {
        return (Func<HookContext, object?>)s_valueReader.MakeGenericMethod(type).Invoke(null, [name])!;
    }

    private static Action<HookContext, object?> Writer(Type type, string name)
    {
        return (Action<HookContext, object?>)s_valueWriter.MakeGenericMethod(type).Invoke(null, [name])!;
    }

    private static Func<HookContext, object?> ValueReader<T>(string name)
        where T : unmanaged
    {
        return context => context.Get<T>(name);
    }

    private static Action<HookContext, object?> ValueWriter<T>(string name)
        where T : unmanaged
    {
        return (context, value) => context.Set(name, (T)value!);
    }

    private static Func<HookContext, object?> ReturnReader<T>()
        where T : unmanaged
    {
        return static context => context.GetReturnValue<T>();
    }

    private static Action<HookContext, object?> ReturnWriter<T>()
        where T : unmanaged
    {
        return static (context, value) => context.SetReturnValue((T)value!);
    }

    private static bool IsValue(Type type)
    {
        return type.IsPrimitive || type.IsEnum;
    }

    private static FProperty? Match(UFunction function, string name)
    {
        foreach (var parameter in function.Parameters)
        {
            if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
                return parameter;
        }

        foreach (var parameter in function.Parameters)
        {
            if (string.Equals(Presented(parameter.Name), name, StringComparison.Ordinal))
                return parameter;
        }

        return null;
    }

    private static string Presented(string engineName)
    {
        return SdkNaming.Parameter(engineName).TrimStart('@');
    }

    private static string Parameters(UFunction function)
    {
        var names = function.Parameters
            .Select(static parameter => Presented(parameter.Name) is var presented && presented == parameter.Name ? parameter.Name : $"{parameter.Name} (or {presented})")
            .ToArray();

        return names.Length is 0 ? "none" : string.Join(", ", names);
    }

    private static string Describe(MethodInfo method)
    {
        return $"{method.DeclaringType?.Name}.{method.Name}";
    }

    private static MethodInfo Accessor(string name)
    {
        return typeof(HookBinder).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
    }
}