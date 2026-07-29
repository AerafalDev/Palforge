using System.Reflection;
using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Sdk;

internal sealed class SdkExtractor
{
    private static readonly IReadOnlySet<string> s_classReserved = Reserved(typeof(UObject));
    private static readonly IReadOnlySet<string> s_structReserved = Reserved(typeof(UStructValue));

    private readonly SdkTypeResolver _resolver;

    public SdkExtractor(SdkTypeResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        _resolver = resolver;
    }

    public SdkEnum Extract(UEnum enumeration)
    {
        var reference = _resolver.Ref(enumeration.Name);
        var members = new List<SdkEnumMember>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (qualified, value) in enumeration.Entries)
            members.Add(new SdkEnumMember(Unique(SdkNaming.Identifier(MemberName(qualified)), seen), value));

        return new SdkEnum(reference?.Namespace ?? SdkNaming.RootNamespace, reference?.Name ?? SdkNaming.Identifier(enumeration.Name), members, EnumUnderlying(members));
    }

    private static string EnumUnderlying(IReadOnlyList<SdkEnumMember> members)
    {
        long min = 0;
        long max = 0;

        foreach (var member in members)
        {
            min = Math.Min(min, member.Value);
            max = Math.Max(max, member.Value);
        }

        if (min >= int.MinValue && max <= int.MaxValue)
            return "int";

        return min >= 0 && max <= uint.MaxValue ? "uint" : "long";
    }

    public SdkClass Extract(UClass klass)
    {
        var reference = _resolver.Ref(klass.Name);
        var typeName = reference?.Name ?? SdkNaming.Identifier(klass.Name);
        var baseName = klass.SuperClass is { } super && _resolver.Qualified(super.Name) is { } qualifiedSuper ? qualifiedSuper : nameof(UObject);
        var properties = new List<SdkProperty>();
        var used = new HashSet<string>(s_classReserved, StringComparer.Ordinal) { typeName };
        SeedInheritedNames(klass, used);

        foreach (var property in klass.Properties)
        {
            if (FactsOf(property) is not { } facts || SdkTypeMap.Map(facts) is not { } accessor)
                continue;

            properties.Add(new SdkProperty(Unique(SdkNaming.Identifier(property.Name), used), accessor.TypeName, accessor.GetBody, accessor.SetBody));
        }

        var methods = new List<SdkMethod>();

        foreach (var function in klass.Functions)
        {
            if (MethodOf(function, klass, used) is { } method)
                methods.Add(method);
        }

        return new SdkClass(reference?.Namespace ?? SdkNaming.RootNamespace, typeName, baseName, properties, methods, klass.Name, IsActor(klass));
    }

    public SdkClass Extract(UScriptStruct structType)
    {
        var reference = _resolver.Ref(structType.Name);
        var typeName = reference?.Name ?? SdkNaming.Identifier(structType.Name);
        var properties = new List<SdkProperty>();
        var used = new HashSet<string>(s_structReserved, StringComparer.Ordinal) { typeName };

        foreach (var property in structType.AllProperties)
        {
            if (FactsOf(property) is not { } facts || SdkTypeMap.Map(facts) is not { } accessor)
                continue;

            properties.Add(new SdkProperty(Unique(SdkNaming.Identifier(property.Name), used), accessor.TypeName, accessor.GetBody, accessor.SetBody));
        }

        return new SdkClass(reference?.Namespace ?? SdkNaming.RootNamespace, typeName, nameof(UStructValue), properties, [], structType.Name, IsStruct: true);
    }

    private void SeedInheritedNames(UClass klass, HashSet<string> used)
    {
        for (var ancestor = klass.SuperClass; ancestor is not null; ancestor = ancestor.SuperClass)
        {
            if (!_resolver.Contains(ancestor.Name))
                continue;

            foreach (var property in ancestor.Properties)
                used.Add(SdkNaming.Identifier(property.Name));

            foreach (var function in ancestor.Functions)
                used.Add(SdkNaming.Identifier(function.Name));
        }
    }

    private static bool IsActor(UClass klass)
    {
        for (UStruct? current = klass; current is not null; current = current.Super)
        {
            if (current.Name is "Actor")
                return true;
        }

        return false;
    }

    private SdkMethod? MethodOf(UFunction function, UClass owner, HashSet<string> used)
    {
        var parameters = new List<SdkParameter>();
        var parameterNames = new HashSet<string>(StringComparer.Ordinal) { "arguments", "outputs", "result" };

        foreach (var parameter in function.Parameters)
        {
            if (FactsOf(parameter) is not { } facts || SdkMethodMap.Parameter(facts, Unique(SdkNaming.Parameter(parameter.Name), parameterNames), parameter.Flags) is not { } mapped)
                return null;

            parameters.Add(mapped);
        }

        var returnType = "void";
        string? reader = null;
        string? returnStruct = null;

        if (function.ReturnParameter is { } returnParameter)
        {
            if (FactsOf(returnParameter) is not { } facts)
                return null;

            if (facts is { Kind: PropertyKind.Struct, ReferencedType: { } structType } && returnParameter is FStructProperty { Struct: { } engineStruct })
            {
                returnType = structType;
                returnStruct = engineStruct.Name;
            }
            else if (SdkMethodMap.Return(facts) is { } mapped)
            {
                returnType = mapped.Type;
                reader = mapped.Reader;
            }
            else
            {
                return null;
            }
        }

        return new SdkMethod(Unique(SdkNaming.Identifier(function.Name), used), function.Name, owner.Name, function.IsStatic, parameters, returnType, reader, returnStruct);
    }

    private SdkPropertyFacts FactsOf(FProperty property)
    {
        return property switch
        {
            FBoolProperty boolean => new SdkPropertyFacts(PropertyKind.Bool, boolean.Offset + boolean.ByteOffset, boolean.ElementSize, BoolMask: boolean.FieldMask),
            FEnumProperty { Enum: { } enumeration } enumProperty => new SdkPropertyFacts(PropertyKind.Enum, enumProperty.Offset, enumProperty.ElementSize, _resolver.Qualified(enumeration.Name)),
            FObjectProperty objectProperty => new SdkPropertyFacts(property.Kind, property.Offset, property.ElementSize, ReferencedClass(objectProperty)),
            FStructProperty { Struct: { } structType } when _resolver.Contains(structType.Name) => new SdkPropertyFacts(PropertyKind.Struct, property.Offset, property.ElementSize, _resolver.Qualified(structType.Name)),
            FArrayProperty { Inner: { } inner } => new SdkPropertyFacts(PropertyKind.Array, property.Offset, property.ElementSize, Element: FactsOf(inner), Name: property.Name),
            FSetProperty { Element: { } setElement } set => new SdkPropertyFacts(PropertyKind.Set, property.Offset, property.ElementSize, Element: FactsOf(setElement), Stride: set.Stride, Name: property.Name),
            FMapProperty { Key: { } key, Value: { } value } map => new SdkPropertyFacts(PropertyKind.Map, property.Offset, property.ElementSize, Key: FactsOf(key), Value: FactsOf(value), Stride: map.PairStride, ValueOffset: map.ValueOffset, Name: property.Name),
            _ => new SdkPropertyFacts(property.Kind, property.Offset, property.ElementSize)
        };
    }

    private static HashSet<string> Reserved(Type baseType)
    {
        var names = new HashSet<string>(StringComparer.Ordinal) { "StaticClass" };

        foreach (var member in baseType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            names.Add(member.Name);

        return names;
    }

    private string? ReferencedClass(FObjectProperty property)
    {
        return property.PropertyClass is { } referenced ? _resolver.Qualified(referenced.Name) : null;
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

    private static string MemberName(string qualified)
    {
        var separator = qualified.LastIndexOf("::", StringComparison.Ordinal);

        return separator >= 0 ? qualified[(separator + 2)..] : qualified;
    }
}