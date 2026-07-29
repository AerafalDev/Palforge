using Microsoft.Extensions.Logging;
using Palforge.Unreal.Reflection;
using Palforge.Unreal.Runtime;

namespace Palforge.Unreal.Sdk;

internal static class SdkGenerator
{
    private const int MaxSdkClasses = 20000;

    public static int Generate(UnrealRuntime runtime, string outputDirectory, IReadOnlyList<string> classNames, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);
        ArgumentNullException.ThrowIfNull(classNames);

        var context = runtime.Reflection;
        var enums = new Dictionary<string, UEnum>(StringComparer.Ordinal);
        var written = 0;

        Directory.CreateDirectory(outputDirectory);

        foreach (var stale in Directory.EnumerateFiles(outputDirectory, "*.cs", SearchOption.AllDirectories))
            File.Delete(stale);

        var classes = new Dictionary<string, UClass>(StringComparer.Ordinal);
        var structs = new Dictionary<string, UScriptStruct>(StringComparer.Ordinal);
        var classQueue = new Queue<UClass>();
        var structQueue = new Queue<UScriptStruct>();

        if (classNames.Contains("*"))
        {
            foreach (var klass in context.AllClasses())
            {
                if (klass.Name is not "Object" && !context.IsReflectionClass(klass.Address))
                    classes.TryAdd(klass.Name, klass);
            }

            foreach (var structType in context.AllScriptStructs())
                structs.TryAdd(structType.Name, structType);

            foreach (var enumeration in context.AllEnums())
                enums.TryAdd(enumeration.Name, enumeration);
        }

        foreach (var className in classNames)
        {
            if (className is "*" || context.FindClass(className) is not { } root)
            {
                if (className is not "*")
                    log.LogWarning("SDK: class '{Class}' was not found in the object array", className);

                continue;
            }

            EnqueueClass(root);
        }

        while ((classQueue.Count > 0 || structQueue.Count > 0) && classes.Count + structs.Count < MaxSdkClasses)
        {
            while (classQueue.Count > 0)
            {
                var klass = classQueue.Dequeue();

                if (!classes.TryAdd(klass.Name, klass))
                    continue;

                EnqueueClass(klass.SuperClass);

                foreach (var property in klass.Properties)
                    Reference(property);

                foreach (var function in klass.Functions)
                {
                    foreach (var parameter in function.Properties)
                    {
                        switch (parameter)
                        {
                            case FStructProperty { Struct: { } parameterStruct }:
                                EnqueueStruct(parameterStruct);
                                break;
                            case FEnumProperty { Enum: { } parameterEnum }:
                                enums.TryAdd(parameterEnum.Name, parameterEnum);
                                break;
                        }
                    }
                }
            }

            while (structQueue.Count > 0)
            {
                var structType = structQueue.Dequeue();

                if (!structs.TryAdd(structType.Name, structType))
                    continue;

                foreach (var property in structType.AllProperties)
                    Reference(property);
            }
        }

        var resolver = new SdkTypeResolver();

        foreach (var klass in classes.Values)
            resolver.Add(klass.Name, SdkNaming.Namespace(context.PackageOf(klass.Address)));

        foreach (var structType in structs.Values)
            resolver.Add(structType.Name, SdkNaming.Namespace(context.PackageOf(structType.Address)));

        foreach (var enumeration in enums.Values)
            resolver.Add(enumeration.Name, SdkNaming.Namespace(context.PackageOf(enumeration.Address)));

        var extractor = new SdkExtractor(resolver);
        var factories = new List<(string UeName, string CsName)>();

        foreach (var klass in classes.Values)
        {
            var model = extractor.Extract(klass);
            written += Write(outputDirectory, model.Namespace, model.Name, SdkEmitter.Emit(model), log);
            factories.Add((klass.Name, resolver.Qualified(klass.Name) ?? model.Name));
        }

        var structFactories = new List<(string UeName, string CsName)>();

        foreach (var structType in structs.Values)
        {
            var model = extractor.Extract(structType);
            written += Write(outputDirectory, model.Namespace, model.Name, SdkEmitter.Emit(model), log);
            structFactories.Add((structType.Name, resolver.Qualified(structType.Name) ?? model.Name));
        }

        foreach (var enumeration in enums.Values)
        {
            var model = extractor.Extract(enumeration);
            written += Write(outputDirectory, model.Namespace, model.Name, SdkEmitter.Emit(model), log);
        }

        written += Write(outputDirectory, SdkNaming.RootNamespace, "SdkRegistry", SdkEmitter.EmitRegistry(factories, structFactories), log);

        log.LogInformation("SDK: generated {Count} files ({Classes} classes, {Structs} structs, {Enums} enums) into {Directory}", written, classes.Count, structs.Count, enums.Count, outputDirectory);

        return written;

        void Reference(FProperty property)
        {
            switch (property)
            {
                case FObjectProperty { PropertyClass: { } klass }:
                    EnqueueClass(klass);
                    break;
                case FStructProperty { Struct: { } structType }:
                    EnqueueStruct(structType);
                    break;
                case FEnumProperty { Enum: { } enumeration }:
                    enums.TryAdd(enumeration.Name, enumeration);
                    break;
                case FArrayProperty { Inner: { } inner }:
                    Reference(inner);
                    break;
                case FSetProperty { Element: { } element }:
                    Reference(element);
                    break;
                case FMapProperty { Key: { } key, Value: { } value }:
                    Reference(key);
                    Reference(value);
                    break;
            }
        }

        void EnqueueStruct(UScriptStruct? candidate)
        {
            if (candidate is not null && !structs.ContainsKey(candidate.Name))
                structQueue.Enqueue(candidate);
        }

        void EnqueueClass(UClass? candidate)
        {
            if (candidate is { Name: not "Object" } && !classes.ContainsKey(candidate.Name) && !context.IsReflectionClass(candidate.Address))
                classQueue.Enqueue(candidate);
        }
    }

    private static int Write(string directory, string @namespace, string typeName, string code, ILogger log)
    {
        try
        {
            var target = Path.Combine(directory, SubdirectoryFor(@namespace));
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, typeName + ".cs"), code);
            return 1;
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "SDK: failed to write '{Type}'", typeName);
            return 0;
        }
    }

    private static string SubdirectoryFor(string @namespace)
    {
        const string prefix = SdkNaming.RootNamespace + ".";

        return @namespace.StartsWith(prefix, StringComparison.Ordinal) ? @namespace[prefix.Length..].Replace('.', Path.DirectorySeparatorChar) : string.Empty;
    }
}