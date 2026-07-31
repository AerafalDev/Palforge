using Palforge.Tests.Memory;

namespace Palforge.Tests.Unreal.Probes;

internal sealed class FakeObjectGraph
{
    private readonly List<nint> _objects = [];
    private readonly Dictionary<string, nint> _classes = new Dictionary<string, nint>(StringComparer.Ordinal);
    private readonly Dictionary<string, nint> _fieldClasses = new Dictionary<string, nint>(StringComparer.Ordinal);
    private readonly Dictionary<string, nint> _functions = new Dictionary<string, nint>(StringComparer.Ordinal);

    public FakeMemory Memory { get; } = new();

    public FakeLayout Layout { get; }

    public FakeNameTable Names { get; } = new();

    public nint ObjectArray { get; private set; }

    public IReadOnlyList<nint> Objects =>
        _objects;

    public nint ClassOfClass =>
        _classes["Class"];

    private FakeObjectGraph(FakeLayout layout)
    {
        Layout = layout;
    }

    public static FakeObjectGraph Build(FakeLayout? layout = null, int filler = 256)
    {
        layout ??= new FakeLayout();
        layout.EnsureCoherent();

        var graph = new FakeObjectGraph(layout);

        graph.Compose(filler);

        return graph;
    }

    public nint ClassNamed(string name)
    {
        return _classes[name];
    }

    public nint FieldClassNamed(string name)
    {
        return _fieldClasses[name];
    }

    public nint FunctionNamed(string name)
    {
        return _functions[name];
    }

    public const uint ClassDefaultObjectFlag = 0x10;

    public nint Package { get; private set; }

    private void Compose(int filler)
    {
        Package = CreateObject("CorePackage", 0, Layout.ObjectSize);

        foreach (var name in (string[])["DoubleProperty", "UInt32Property", "ObjectProperty", "StructProperty"])
            _fieldClasses[name] = CreateFieldClass(name);

        var chain = new (string Name, string? Super, int Size)[]
        {
            ("Object", null, 0x28),
            ("Field", "Object", 0x30),
            ("Struct", "Field", 0xB0),
            ("Class", "Struct", 0x200),
            ("ScriptStruct", "Struct", 0xC0),
            ("Function", "Struct", 0xE0),
            ("Enum", "Field", 0x60),
            ("Package", "Object", 0x38)
        };

        foreach (var (name, super, size) in chain)
            _classes[name] = CreateClass(name, super, size);

        CreateFunctions("Object", [("ExecuteUbergraph", 1), ("PostInitProperties", 0)]);
        CreateFunctions("Struct", [("Link", 2)]);
        CreateFunctions("Class", [("CreateDefaultObject", 3), ("PurgeClass", 0), ("GetDefaultObject", 1)]);

        CreateEnum("EEngineMode", ["Editor", "Game", "Server"]);
        CreateEnum("ENetRole", ["None", "SimulatedProxy", "AutonomousProxy", "Authority"]);

        CreateScriptStruct(new FakeStructSpec("Vector", 0x18,
        [
            new FakePropertySpec("X", "DoubleProperty", 0x00, 8),
            new FakePropertySpec("Y", "DoubleProperty", 0x08, 8),
            new FakePropertySpec("Z", "DoubleProperty", 0x10, 8)
        ]));

        CreateScriptStruct(new FakeStructSpec("Guid", 0x10,
        [
            new FakePropertySpec("A", "UInt32Property", 0x00, 4),
            new FakePropertySpec("B", "UInt32Property", 0x04, 4),
            new FakePropertySpec("C", "UInt32Property", 0x08, 4),
            new FakePropertySpec("D", "UInt32Property", 0x0C, 4)
        ]));

        CreateScriptStruct(new FakeStructSpec("Transform", 0x20,
        [
            new FakePropertySpec("Translation", "StructProperty", 0x00, 0x18, "Vector"),
            new FakePropertySpec("Holder", "ObjectProperty", 0x18, 8, "Class")
        ]));

        foreach (var (name, _, _) in chain)
            CreateDefaultObject(name);

        for (var index = 0; index < filler; index++)
            CreateObject($"Filler_{index}", _classes["Object"], Layout.ObjectSize);

        Publish();
    }

    private static ulong CastFlagFor(string name)
    {
        return name switch
        {
            "DoubleProperty" => 0x0000000100000000,
            "UInt32Property" => 0x0000000000000800,
            "ObjectProperty" => 0x0000000000010000,
            "StructProperty" => 0x0000000000100000,
            _ => 0
        };
    }

    private nint CreateFieldClass(string name)
    {
        var address = Memory.Allocate(Layout.FieldClassSize);

        Memory.TryWrite(address + Layout.FieldClassName, Names.Intern(name));
        Memory.TryWrite(address + Layout.FieldClassCastFlags, CastFlagFor(name));

        return address;
    }

    private nint CreateClass(string name, string? super, int propertiesSize)
    {
        var address = CreateObject(name, 0, Layout.ClassSize);

        Memory.TryWrite(address + Layout.SuperStruct, super is null ? 0 : _classes[super]);
        Memory.TryWrite(address + Layout.PropertiesSize, propertiesSize);
        Memory.TryWrite(address + Layout.MinAlignment, 8);
        Memory.TryWrite(address + Layout.ClassFlags, 0x2000_0000u);
        Memory.TryWrite(address + Layout.ClassCastFlags, 0UL);

        return address;
    }

    private void CreateScriptStruct(FakeStructSpec spec)
    {
        var address = CreateObject(spec.Name, _classes["ScriptStruct"], Layout.StructSize);

        Memory.TryWrite(address + Layout.SuperStruct, 0);
        Memory.TryWrite(address + Layout.PropertiesSize, spec.PropertiesSize);
        Memory.TryWrite(address + Layout.MinAlignment, 8);

        _classes[spec.Name] = address;

        nint previous = 0;

        foreach (var property in spec.Properties)
        {
            var field = CreateProperty(property, address);

            if (previous is 0)
                Memory.TryWrite(address + Layout.ChildProperties, field);
            else
                Memory.TryWrite(previous + Layout.FFieldNext, field);

            previous = field;
        }
    }

    private nint CreateProperty(FakePropertySpec spec, nint owner)
    {
        var address = Memory.Allocate(Layout.PropertyBaseSize + 0x40);

        Memory.TryWrite(address, unchecked((nint)0x0000_7FFF_0000_1000L));
        Memory.TryWrite(address + Layout.FFieldClassPrivate, _fieldClasses[spec.FieldClass]);
        Memory.TryWrite(address + Layout.FFieldOwner, owner);
        Memory.TryWrite(address + Layout.FFieldNext, 0);
        Memory.TryWrite(address + Layout.FFieldNamePrivate, Names.Intern(spec.Name));
        Memory.TryWrite(address + Layout.FFieldFlags, 0);

        Memory.TryWrite(address + Layout.ArrayDim, 1);
        Memory.TryWrite(address + Layout.ElementSize, spec.ElementSize);
        Memory.TryWrite(address + Layout.PropertyFlags, 0x0000_0010_4000_0200UL);
        Memory.TryWrite(address + Layout.RepIndex, (ushort)0);
        Memory.TryWrite(address + Layout.OffsetInternal, spec.Offset);
        Memory.TryWrite(address + Layout.PropertyLinkNext, 0);

        if (spec.Target is not null)
            Memory.TryWrite(address + Layout.PropertyBaseSize, _classes[spec.Target]);

        return address;
    }

    private void CreateEnum(string name, string[] members)
    {
        var address = CreateObject(name, _classes["Enum"], Layout.StructSize);
        var pairs = Memory.Allocate(members.Length * Layout.EnumPairStride);

        _classes[name] = address;

        for (var index = 0; index < members.Length; index++)
        {
            Memory.TryWrite(pairs + index * Layout.EnumPairStride, Names.Intern($"{name}::{members[index]}"));
            Memory.TryWrite(pairs + index * Layout.EnumPairStride + 4, 0);
            Memory.TryWrite(pairs + index * Layout.EnumPairStride + 8, (long)index);
        }

        Memory.TryWrite(address + Layout.EnumNames, pairs);
        Memory.TryWrite(address + Layout.EnumNames + nint.Size, members.Length);
        Memory.TryWrite(address + Layout.EnumNames + nint.Size + 4, members.Length);
    }

    private void CreateFunctions(string className, (string Name, int Parameters)[] names)
    {
        var owner = _classes[className];

        nint previous = 0;

        foreach (var (name, parameters) in names)
        {
            var function = CreateObject($"{className}.{name}", _classes["Function"], Layout.StructSize);

            Memory.TryWrite(function + Layout.OuterPrivate, owner);
            Memory.TryWrite(function + Layout.SuperStruct, 0);
            Memory.TryWrite(function + Layout.PropertiesSize, 0x10);
            Memory.TryWrite(function + Layout.MinAlignment, 8);
            Memory.TryWrite(function + Layout.FieldNext, 0);

            nint tail = 0;

            for (var index = 0; index < parameters; index++)
            {
                var parameter = CreateProperty(new FakePropertySpec($"Parm{index}", "UInt32Property", index * 4, 4), function);

                Memory.TryWrite(parameter + Layout.PropertyFlags, 0x80UL);

                if (tail is 0)
                    Memory.TryWrite(function + Layout.ChildProperties, parameter);
                else
                    Memory.TryWrite(tail + Layout.FFieldNext, parameter);

                tail = parameter;
            }

            _functions[$"{className}.{name}"] = function;

            Memory.TryWrite(function + Layout.FunctionNumParms, (ushort)parameters);
            Memory.TryWrite(function + Layout.FunctionParmsSize, (ushort)(parameters * 4));

            if (previous is 0)
                Memory.TryWrite(owner + Layout.Children, function);
            else
                Memory.TryWrite(previous + Layout.FieldNext, function);

            previous = function;
        }
    }

    private void CreateDefaultObject(string className)
    {
        var cdo = CreateObject($"Default__{className}", _classes[className], Layout.ClassSize);

        Memory.TryWrite(cdo + Layout.ObjectFlags, ClassDefaultObjectFlag);
        Memory.TryWrite(_classes[className] + Layout.ClassDefaultObject, cdo);
    }

    private nint CreateObject(string name, nint klass, int size)
    {
        var address = Memory.Allocate(size);

        Memory.TryWrite(address, unchecked((nint)0x0000_7FFF_0000_2000L));
        Memory.TryWrite(address + Layout.ObjectFlags, 0u);
        Memory.TryWrite(address + Layout.InternalIndex, _objects.Count);
        Memory.TryWrite(address + Layout.ClassPrivate, klass);
        Memory.TryWrite(address + Layout.NamePrivate, Names.Intern(name));
        Memory.TryWrite(address + Layout.NamePrivate + 4, 0);
        Memory.TryWrite(address + Layout.OuterPrivate, Package);

        _objects.Add(address);

        return address;
    }

    private void Publish()
    {
        var classes = _classes.Values.ToHashSet();

        foreach (var address in _objects)
        {
            if (!Memory.TryRead(address + Layout.ClassPrivate, out nint current) || current is not 0)
                continue;

            Memory.TryWrite(address + Layout.ClassPrivate, classes.Contains(address) ? _classes["Class"] : _classes["Package"]);
        }

        var chunks = (_objects.Count + Layout.ElementsPerChunk - 1) / Layout.ElementsPerChunk;
        var maxChunks = Math.Max(Layout.MaxChunks, chunks);
        var table = Memory.Allocate(maxChunks * nint.Size);

        for (var chunk = 0; chunk < chunks; chunk++)
        {
            var items = Memory.Allocate(Layout.ElementsPerChunk * Layout.ItemStride);

            Memory.TryWrite(table + chunk * nint.Size, items);

            for (var slot = 0; slot < Layout.ElementsPerChunk; slot++)
            {
                var index = chunk * Layout.ElementsPerChunk + slot;
                var item = items + slot * Layout.ItemStride;

                Memory.TryWrite(item + Layout.ItemObject, index < _objects.Count ? _objects[index] : 0);
                Memory.TryWrite(item + Layout.ItemFlags, 0);
                Memory.TryWrite(item + Layout.ItemSerialNumber, index + 1);
            }
        }

        ObjectArray = Memory.Allocate(Layout.ObjectArraySize);

        var chunked = ObjectArray + Layout.ObjObjects;

        Memory.TryWrite(chunked + Layout.ChunkedObjects, table);
        Memory.TryWrite(chunked + Layout.ChunkedMaxElements, maxChunks * Layout.ElementsPerChunk);
        Memory.TryWrite(chunked + Layout.ChunkedNumElements, _objects.Count);
        Memory.TryWrite(chunked + Layout.ChunkedMaxChunks, maxChunks);
        Memory.TryWrite(chunked + Layout.ChunkedNumChunks, chunks);
    }
}