using System.Text;

namespace Palforge.Unreal.Sdk;

internal static class SdkNaming
{
    public const string RootNamespace = "Palforge.Sdk";

    private static readonly HashSet<string> s_keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint",
        "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    };

    public static string Namespace(string packageDirectory)
    {
        ArgumentNullException.ThrowIfNull(packageDirectory);

        var segments = packageDirectory
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Identifier);

        var joined = string.Join('.', segments);

        return joined.Length is 0 ? RootNamespace : $"{RootNamespace}.{joined}";
    }

    public static string Identifier(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var builder = new StringBuilder(name.Length);
        var segment = new StringBuilder();

        foreach (var character in name)
        {
            if (char.IsAsciiLetterOrDigit(character))
                segment.Append(character);
            else
                FlushSegment();
        }

        FlushSegment();

        if (builder.Length is 0)
            return "_";

        if (char.IsAsciiDigit(builder[0]))
            builder.Insert(0, '_');

        var identifier = builder.ToString();

        return s_keywords.Contains(identifier) ? '@' + identifier : identifier;

        void FlushSegment()
        {
            if (segment.Length is 0)
                return;

            var text = segment.ToString();
            builder.Append(char.ToUpperInvariant(text[0]));

            if (text.Length > 1)
                builder.Append(IsAllUpper(text) ? text[1..].ToLowerInvariant() : text[1..]);

            segment.Clear();
        }
    }

    private static bool IsAllUpper(string text)
    {
        foreach (var character in text)
        {
            if (char.IsAsciiLetterLower(character))
                return false;
        }

        return true;
    }

    public static string Parameter(string name)
    {
        var identifier = Identifier(name);

        if (identifier[0] is '_' or '@' || !char.IsAsciiLetterUpper(identifier[0]))
            return identifier;

        var camelCase = char.ToLowerInvariant(identifier[0]) + identifier[1..];

        return s_keywords.Contains(camelCase) ? '@' + camelCase : camelCase;
    }
}