using Palforge.Unreal.Reflection;
using Palforge.Unreal.Runtime;

namespace Palforge.Unreal.Api;

public sealed class UnrealApi
{
    private readonly UnrealRuntime _runtime;

    public bool IsReady =>
        _runtime.IsReady;

    internal UnrealApi(UnrealRuntime runtime)
    {
        _runtime = runtime;
    }

    public T? FindFirstOf<T>()
        where T : UObject
    {
        return FindFirstOf(EngineNameOf<T>()) as T;
    }

    public T FindFirstOfOrThrow<T>()
        where T : UObject
    {
        return FindFirstOf<T>() ?? throw new InvalidOperationException($"{typeof(T).Name} is not a generated SDK type, so it names no engine class. Use the string form, or regenerate the SDK if the class is new.");
    }

    public IEnumerable<T> FindAll<T>()
        where T : UObject
    {
        foreach (var instance in FindAllOf(EngineNameOf<T>()))
        {
            if (instance is T typed)
                yield return typed;
        }
    }

    public UClass? ClassOf<T>()
        where T : UObject
    {
        return FindClass(EngineNameOf<T>());
    }

    public UClass ClassOfOrThrow<T>()
        where T : UObject
    {
        return ClassOf<T>() ?? throw new InvalidOperationException($"{typeof(T).Name} is not a generated SDK type, so it names no engine class. Use the string form, or regenerate the SDK if the class is new.");
    }

    public UClass? FindClass(string className)
    {
        ArgumentException.ThrowIfNullOrEmpty(className);

        return _runtime.Reflection.FindClass(className);
    }

    public UClass FindClassOrThrow(string className)
    {
        return FindClass(className) ?? throw new InvalidOperationException($"No class named {className}.");
    }

    public UObject? FindFirstOf(string className)
    {
        ArgumentException.ThrowIfNullOrEmpty(className);

        return _runtime.Reflection.FindFirstOf(className);
    }

    public UObject FindFirstOfOrThrow(string className)
    {
        return FindFirstOf(className) ?? throw new InvalidOperationException($"No object of class {className}.");
    }

    public IEnumerable<UObject> FindAllOf(string className)
    {
        ArgumentException.ThrowIfNullOrEmpty(className);

        return _runtime.Reflection.FindAllOf(className);
    }

    public UObject? FindObject(string objectName)
    {
        ArgumentException.ThrowIfNullOrEmpty(objectName);

        return _runtime.Reflection.FindObject(objectName);
    }

    public UObject FindObjectOrThrow(string objectName)
    {
        return FindObject(objectName) ?? throw new InvalidOperationException($"No object named {objectName}.");
    }

    public UObject? LoadAsset(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        return _runtime.Reflection.LoadAsset(path, asClass: false);
    }

    public UObject LoadAssetOrThrow(string path)
    {
        return LoadAsset(path) ?? throw new InvalidOperationException($"No asset at {path}.");
    }

    public UClass? LoadClass(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        return _runtime.Reflection.LoadAsset(path, asClass: true) as UClass;
    }

    public UClass LoadClassOrThrow(string path)
    {
        return LoadClass(path) ?? throw new InvalidOperationException($"No class at {path}.");
    }

    public IEnumerable<KeyValuePair<string, UStructValue>> Rows(UObject table)
    {
        ArgumentNullException.ThrowIfNull(table);

        return _runtime.Reflection.DataTableRows(table.Address);
    }

    public UScriptStruct? RowType(UObject table)
    {
        ArgumentNullException.ThrowIfNull(table);

        return _runtime.Reflection.DataTableRowType(table.Address);
    }

    public UScriptStruct RowTypeOrThrow(UObject table)
    {
        return RowType(table) ?? throw new InvalidOperationException($"Data table {table.Name} has no row type.");
    }

    public UStructValue? Row(UObject table, string rowName)
    {
        ArgumentException.ThrowIfNullOrEmpty(rowName);

        foreach (var (name, row) in Rows(table))
        {
            if (string.Equals(name, rowName, StringComparison.Ordinal))
                return row;
        }

        return null;
    }

    public UStructValue RowOrThrow(UObject table, string rowName)
    {
        return Row(table, rowName) ?? throw new InvalidOperationException($"Data table {table.Name} has no row named {rowName}.");
    }

    public IEnumerable<UClass> AllClasses()
    {
        return _runtime.Reflection.AllClasses();
    }

    public IEnumerable<UScriptStruct> AllStructs()
    {
        return _runtime.Reflection.AllScriptStructs();
    }

    public IEnumerable<UEnum> AllEnums()
    {
        return _runtime.Reflection.AllEnums();
    }

    public string PackageOf(UObject value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return _runtime.Reflection.PackageOf(value.Address);
    }

    public string? EngineNameOf(Type wrapper)
    {
        ArgumentNullException.ThrowIfNull(wrapper);

        return _runtime.Reflection.EngineNameOf(wrapper);
    }

    public string EngineNameOfOrThrow(Type wrapper)
    {
        return EngineNameOf(wrapper) ?? throw new InvalidOperationException($"{wrapper.Name} is not a generated SDK type, so it names no engine class. Use the string form, or regenerate the SDK if the class is new.");
    }

    private string EngineNameOf<T>()
        where T : UObject
    {
        return EngineNameOf(typeof(T)) ?? throw new InvalidOperationException($"{typeof(T).Name} is not a generated SDK type, so it names no engine class. Use the string form, or regenerate the SDK if the class is new.");
    }
}