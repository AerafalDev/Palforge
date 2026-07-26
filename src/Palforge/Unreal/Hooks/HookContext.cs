using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Hooks;

public sealed class HookContext
{
    private readonly UnrealContext _context;
    private readonly nint _self;
    private readonly nint _function;
    private readonly nint _parms;

    public UObject Self =>
        _context.Wrap(_self);

    public UFunction Function =>
        new(_function, _context);

    public nint Frame =>
        _parms;

    internal UnrealContext Reflection =>
        _context;

    public bool OriginalSkipped { get; private set; }

    internal HookEntry[]? Handlers { get; set; }

    internal HookContext(UnrealContext context, nint self, nint function, nint parms)
    {
        _context = context;
        _self = self;
        _function = function;
        _parms = parms;
    }

    public void SkipOriginal()
    {
        OriginalSkipped = true;
    }

    public T Get<T>(string parameterName)
        where T : unmanaged
    {
        return Parameter(parameterName) is { } parameter && _context.TryReadValue(_parms + parameter.Offset, out T value) ? value : default;
    }

    public bool TryGet<T>(string parameterName, out T value)
        where T : unmanaged
    {
        if (Parameter(parameterName) is { } parameter && _context.TryReadValue(_parms + parameter.Offset, out value))
            return true;

        value = default;

        return false;
    }

    public bool Set<T>(string parameterName, in T value)
        where T : unmanaged
    {
        return Parameter(parameterName) is { } parameter && _context.WriteValue(_parms + parameter.Offset, value);
    }

    public UObject? GetObject(string parameterName)
    {
        return Parameter(parameterName) is FObjectProperty parameter ? parameter.GetObjectAt(_parms) : null;
    }

    public bool SetObject(string parameterName, UObject? value)
    {
        return Parameter(parameterName) is FObjectProperty parameter && parameter.SetObjectAt(_parms, value);
    }

    public string GetString(string parameterName)
    {
        return ReadString(Parameter(parameterName));
    }

    public bool SetString(string parameterName, string value)
    {
        return WriteString(Parameter(parameterName), value);
    }

    public UStructValue? GetStruct(string parameterName)
    {
        return Parameter(parameterName) is FStructProperty { Struct: { } type } parameter
            ? _context.WrapStruct(_parms + parameter.Offset, type.Name)
            : null;
    }

    public FProperty? GetParameter(string parameterName)
    {
        return Parameter(parameterName);
    }

    internal nint StructAddress(string parameterName)
    {
        return Parameter(parameterName) is FStructProperty parameter ? _parms + parameter.Offset : 0;
    }

    public T GetReturnValue<T>()
        where T : unmanaged
    {
        return Function.ReturnParameter is { } parameter && _context.TryReadValue(_parms + parameter.Offset, out T value) ? value : default;
    }

    public bool SetReturnValue<T>(in T value)
        where T : unmanaged
    {
        return Function.ReturnParameter is { } parameter && _context.WriteValue(_parms + parameter.Offset, value);
    }

    public UObject? GetReturnObject()
    {
        return Function.ReturnParameter is FObjectProperty parameter ? parameter.GetObjectAt(_parms) : null;
    }

    public bool SetReturnObject(UObject? value)
    {
        return Function.ReturnParameter is FObjectProperty parameter && parameter.SetObjectAt(_parms, value);
    }

    public string GetReturnString()
    {
        return ReadString(Function.ReturnParameter);
    }

    public bool SetReturnString(string value)
    {
        return WriteString(Function.ReturnParameter, value);
    }

    private FProperty? Parameter(string name)
    {
        return Function.FindProperty(name);
    }

    private string ReadString(FProperty? property)
    {
        return property switch
        {
            FStrProperty parameter => _context.ReadFString(_parms + parameter.Offset),
            FNameProperty parameter => _context.NameAt(_parms + parameter.Offset),
            FTextProperty parameter => _context.TextValue(_parms + parameter.Offset),
            _ => string.Empty
        };
    }

    private bool WriteString(FProperty? property, string value)
    {
        return property switch
        {
            FStrProperty parameter => _context.WriteFString(_parms + parameter.Offset, value),
            FNameProperty parameter => _context.WriteName(_parms + parameter.Offset, value),
            _ => false
        };
    }
}