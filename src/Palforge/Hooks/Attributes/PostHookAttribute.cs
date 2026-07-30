namespace Palforge.Hooks.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class PostHookAttribute : HookAttribute
{
    public PostHookAttribute(string target) : base(target)
    {
    }

    public PostHookAttribute(string className, string functionName) : base(className, functionName)
    {
    }

    public PostHookAttribute(Type wrapper, string functionName) : base(wrapper, functionName)
    {
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostHookAttribute<T> : PostHookAttribute
    where T : class
{
    public PostHookAttribute(string functionName) : base(typeof(T), functionName)
    {
    }
}