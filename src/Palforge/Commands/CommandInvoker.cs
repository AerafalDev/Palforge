using System.Linq.Expressions;

namespace Palforge.Commands;

internal static class CommandInvoker
{
    public static Action<object, object?[]> Compile(System.Reflection.MethodInfo method)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var arguments = Expression.Parameter(typeof(object?[]), "arguments");

        var parameters = method.GetParameters();
        var call = new Expression[parameters.Length];

        for (var index = 0; index < parameters.Length; index++)
            call[index] = Expression.Convert(Expression.ArrayIndex(arguments, Expression.Constant(index)), parameters[index].ParameterType);

        var invocation = Expression.Call(Expression.Convert(instance, method.DeclaringType!), method, call);

        return Expression.Lambda<Action<object, object?[]>>(invocation, instance, arguments).Compile();
    }
}