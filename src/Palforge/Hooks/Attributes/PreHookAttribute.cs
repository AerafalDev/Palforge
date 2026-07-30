namespace Palforge.Hooks.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class PreHookAttribute : HookAttribute
{
    public PreHookAttribute(string target) : base(target)
    {
    }

    public PreHookAttribute(string className, string functionName) : base(className, functionName)
    {
    }

    public PreHookAttribute(Type wrapper, string functionName) : base(wrapper, functionName)
    {
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class PreHookAttribute<T> : PreHookAttribute
    where T : class
{
    public PreHookAttribute(string functionName) : base(typeof(T), functionName)
    {
    }
}