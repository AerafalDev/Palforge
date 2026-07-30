namespace Palforge.Hooks.Attributes;

public abstract class HookAttribute : Attribute
{
    private const char Separator = ':';

    public string? ClassName { get; }

    public Type? Wrapper { get; }

    public string FunctionName { get; }

    public bool IncludeOverrides { get; set; }

    protected HookAttribute(string target)
    {
        ArgumentException.ThrowIfNullOrEmpty(target);

        var separator = target.IndexOf(Separator, StringComparison.Ordinal);

        if (separator <= 0 || separator == target.Length - 1)
            throw new ArgumentException($"'{target}' is not a hook target — write it as \"Class{Separator}Function\"", nameof(target));

        ClassName = target[..separator];
        FunctionName = target[(separator + 1)..];
    }

    protected HookAttribute(string className, string functionName)
    {
        ArgumentException.ThrowIfNullOrEmpty(className);
        ArgumentException.ThrowIfNullOrEmpty(functionName);

        ClassName = className;
        FunctionName = functionName;
    }

    protected HookAttribute(Type wrapper, string functionName)
    {
        ArgumentNullException.ThrowIfNull(wrapper);
        ArgumentException.ThrowIfNullOrEmpty(functionName);

        Wrapper = wrapper;
        FunctionName = functionName;
    }
}