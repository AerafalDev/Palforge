namespace Palforge.Unreal.Sdk;

internal sealed class SdkTypeResolver
{
    private readonly Dictionary<string, SdkTypeRef> _byUeName;
    private readonly Dictionary<string, HashSet<string>> _namesPerNamespace;

    public SdkTypeResolver()
    {
        _byUeName = new Dictionary<string, SdkTypeRef>(StringComparer.Ordinal);
        _namesPerNamespace = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    }

    public SdkTypeRef Add(string ueName, string @namespace)
    {
        if (_byUeName.TryGetValue(ueName, out var existing))
            return existing;

        var used = _namesPerNamespace.TryGetValue(@namespace, out var set) ? set : _namesPerNamespace[@namespace] = new HashSet<string>(StringComparer.Ordinal);
        var name = Unique(SdkNaming.Identifier(ueName), used);
        var reference = new SdkTypeRef(@namespace, name, $"{@namespace}.{name}");

        _byUeName[ueName] = reference;

        return reference;
    }

    public SdkTypeRef? Ref(string ueName)
    {
        return _byUeName.GetValueOrDefault(ueName);
    }

    public string? Qualified(string ueName)
    {
        return _byUeName.TryGetValue(ueName, out var reference) ? reference.Qualified : null;
    }

    public bool Contains(string ueName)
    {
        return _byUeName.ContainsKey(ueName);
    }

    private static string Unique(string name, HashSet<string> used)
    {
        if (used.Add(name))
            return name;

        for (var index = 2; ; index++)
        {
            var candidate = name + index;

            if (used.Add(candidate))
                return candidate;
        }
    }
}