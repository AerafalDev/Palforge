using System.Reflection;
using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Sdk;

internal static class SdkEnv
{
    private static volatile UnrealContext? s_context;

    internal static UnrealContext? Context
    {
        get => s_context;
        set => s_context = value;
    }

    internal static UClass? StaticClass(string className)
    {
        return Context?.FindClass(className);
    }

    internal static UObject? SpawnActor(string className, UObject? owner, UStructValue? at = null)
    {
        if (Context is not { } context || context.FindClass(className) is not { } klass)
            return null;

        var transform = at is null ? [] : context.BytesAt(at.Address, context.StructSizeOf(at));

        return context.SpawnActor(klass.Address, owner?.Address ?? 0, transform);
    }

    internal static UObject? New(string className, UObject? outer)
    {
        return Context is { } context && context.FindClass(className) is { } klass ? context.SpawnObject(klass.Address, outer?.Address ?? 0) : null;
    }

    internal static IEnumerable<KeyValuePair<string, UStructValue>> Rows(UObject table)
    {
        return Context is { } context ? context.DataTableRows(table.Address) : [];
    }

    internal static IEnumerable<KeyValuePair<string, T>> Rows<T>(UObject table)
        where T : UStructValue
    {
        foreach (var row in Rows(table))
        {
            if (row.Value is T typed)
                yield return new KeyValuePair<string, T>(row.Key, typed);
        }
    }

    internal static UScriptStruct? RowType(UObject table)
    {
        return Context?.DataTableRowType(table.Address);
    }

    internal static UStructValue? Row(UObject table, string rowName)
    {
        foreach (var row in Rows(table))
        {
            if (string.Equals(row.Key, rowName, StringComparison.Ordinal))
                return row.Value;
        }

        return null;
    }

    internal static UObject? LoadAsset(string path)
    {
        return Context?.LoadAsset(path, asClass: false);
    }

    internal static byte[] NameBytes(string value)
    {
        return Context?.NameBytes(value) ?? new byte[sizeof(long)];
    }

    internal static byte[] StructBytes(UnrealValueBase? value, int size)
    {
        return Context is { } context && value is not null ? context.BytesAt(value.Address, size) : new byte[size];
    }

    internal static UObject? Wrap(byte[]? data)
    {
        if (Context is not { } context || data is not { Length: >= 8 })
            return null;

        var pointer = (nint)BitConverter.ToInt64(data, 0);

        return pointer is 0 ? null : context.WrapOrNull(pointer);
    }

    internal static T? AllocateStruct<T>(string structName)
        where T : UStructValue
    {
        if (Context is not { } context || context.FindScriptStruct(structName) is not { } type)
            return null;

        var address = context.AllocateStruct(type.Address);

        if (address is 0)
            return null;

        var value = (T)Activator.CreateInstance(typeof(T), BindingFlags.NonPublic | BindingFlags.Instance, null, [address, context], null)!;

        value.Own(type.Address);

        return value;
    }

    internal static T? CallForStruct<T>(UObject target, string functionName, string structName, params byte[][] arguments)
        where T : UStructValue
    {
        return CallForStruct<T>(target, functionName, structName, arguments, out _);
    }

    internal static T? CallStaticForStruct<T>(string className, string functionName, string structName, params byte[][] arguments)
        where T : UStructValue
    {
        return CallStaticForStruct<T>(className, functionName, structName, arguments, out _);
    }

    internal static T? CallForStruct<T>(UObject target, string functionName, string structName, byte[][] arguments, out byte[][] outputs)
        where T : UStructValue
    {
        outputs = [];

        if (target.Class?.FindFunction(functionName) is not { } function || AllocateStruct<T>(structName) is not { } result)
            return null;

        function.Invoke(target, arguments, null, result.Address, out outputs);
        return result;
    }

    internal static T? CallStaticForStruct<T>(string className, string functionName, string structName, byte[][] arguments, out byte[][] outputs)
        where T : UStructValue
    {
        outputs = [];

        if (!TryResolve(className, functionName, out var target, out var function) || AllocateStruct<T>(structName) is not { } result)
            return null;

        function.Invoke(target, arguments, null, result.Address, out outputs);

        return result;
    }

    internal static T[] Objects<T>(byte[]? data)
        where T : UObject
    {
        if (Context is not { } context || data is not { Length: >= 8 })
            return [];

        var items = new List<T>(data.Length / 8);

        for (var offset = 0; offset + 8 <= data.Length; offset += 8)
        {
            if ((nint)BitConverter.ToInt64(data, offset) is var pointer and not 0 && context.WrapOrNull(pointer) is T typed)
                items.Add(typed);
        }

        return [.. items];
    }

    internal static T[] Values<T>(byte[]? data)
        where T : unmanaged
    {
        return data is { Length: > 0 } ? System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(data).ToArray() : [];
    }

    internal static string Text(byte[]? data)
    {
        return data is { Length: > 0 } ? System.Text.Encoding.Unicode.GetString(data) : string.Empty;
    }

    internal static byte[]? CallStatic(string className, string functionName, params byte[][] arguments)
    {
        if (!TryResolve(className, functionName, out var target, out var function))
            return null;

        return function.Invoke(target, arguments);
    }

    internal static byte[]? CallStatic(string className, string functionName, byte[][] arguments, out byte[][] outputs)
    {
        if (!TryResolve(className, functionName, out var target, out var function))
        {
            outputs = [];
            return null;
        }

        return function.Invoke(target, arguments, out outputs);
    }

    internal static byte[]? CallStatic(string className, string functionName, byte[][] arguments, nint[] destinations, out byte[][] outputs)
    {
        if (!TryResolve(className, functionName, out var target, out var function))
        {
            outputs = [];
            return null;
        }

        return function.Invoke(target, arguments, destinations, out outputs);
    }

    private static bool TryResolve(string className, string functionName, out UObject target, out UFunction function)
    {
        if (Context is { } context
            && context.FindClass(className) is { ClassDefaultObject: { } cdo } klass
            && klass.FindFunction(functionName) is { } resolved)
        {
            target = cdo;
            function = resolved;
            return true;
        }

        target = null!;
        function = null!;
        return false;
    }
}