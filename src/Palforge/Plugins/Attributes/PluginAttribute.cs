namespace Palforge.Plugins.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PluginAttribute : Attribute
{
    public string Id { get; }

    public string? Name { get; init; }

    public string? Author { get; init; }

    public string? Version { get; init; }

    public PluginAttribute(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
    }
}