using Palforge.Unreal.Threading;

namespace Palforge.Unreal.Reflection;

public class UStructValue : UnrealValueBase, IDisposable
{
    private readonly string? _structName;

    private nint _owned;

    public UScriptStruct? StructType =>
        _structName is { } name ? Context.FindScriptStruct(name) : null;

    internal UStructValue(nint address, UnrealContext context, string? structName = null) : base(address, context)
    {
        _structName = structName;
        GC.SuppressFinalize(this);
    }

    ~UStructValue()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    internal override FProperty? FindOwnProperty(string name)
    {
        return StructType?.FindProperty(name);
    }

    internal void Own(nint structType)
    {
        _owned = structType;

        GC.ReRegisterForFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        var structType = Interlocked.Exchange(ref _owned, 0);

        if (structType is 0)
            return;

        if (disposing)
            Context.DestroyStruct(structType, Address);
        else
            Reclaim(Context, structType, Address);
    }

    private static void Reclaim(UnrealContext context, nint structType, nint address)
    {
        if (!GameThread.IsAvailable)
            return;

        GameThread.Post(() => context.DestroyStruct(structType, address));
    }
}