using System.Collections.Concurrent;
using System.Numerics;
using System.Text;
using Palforge.Images;
using Palforge.Layout;
using Palforge.Memory;
using Palforge.Unreal.Names;
using Palforge.Unreal.Probes;
using Palforge.Unreal.Threading;

namespace Palforge.Unreal.Reflection;

internal sealed class UnrealContext
{
    private const int MaxSuperDepth = 64;
    private const int MaxStringChars = 0x100000;
    private const int MaxEnumEntries = 0x10000;
    private const int EnumPairStride = 16;
    private const int MaxSaneCount = 100_000_000;
    private const int MaxFrameSize = 0x10000;

    private const int SparseDataOffset = 0x00;       // FScriptArray.Data
    private const int SparseNumOffset = 0x08;        // FScriptArray.Num (allocated slots, holes included)
    private const int SparseBitInlineOffset = 0x10;  // FScriptBitArray inline TInlineAllocator<4> words
    private const int SparseBitHeapOffset = 0x20;    // FScriptBitArray secondary heap pointer
    private const int SparseBitNumOffset = 0x28;     // FScriptBitArray.NumBits
    private const int SparseBitMaxOffset = 0x2C;     // FScriptBitArray.MaxBits
    private const int SparseFirstFreeOffset = 0x30;  // FirstFreeIndex
    private const int SparseNumFreeOffset = 0x34;    // NumFreeIndices
    private const int SparseInlineWords = 4;         // 128 bits inline before the heap is used

    private const int SetHashInlineOffset = 0x38;    // inline FSetElementId[1] bucket
    private const int SetHashHeapOffset = 0x40;      // secondary (heap) bucket array pointer
    private const int SetHashSizeOffset = 0x48;      // int32 HashSize (power of two, or 1)
    private const int ScriptMapSize = 0x50;          // the whole of the above: HashSize ends at 0x4C, aligned to 8
    private const int MaxHashChain = 1 << 24;        // bucket-walk guard against a corrupt chain
    private const int InvalidId = -1;                // FSetElementId::INDEX_NONE
    private const int MaxExampleScan = 80000;        // object-graph budget for FindLiveExamples
    private const int TextDisplayStringSlot = 0x10;   // ITextData::GetDisplayString vtable byte offset
    private const int MulticastGetDelegateSlot = 0x168; // FMulticastDelegateProperty::GetMulticastDelegate (RE-UE4SS 5_01)
    private const int ScriptDelegateStride = 16;
    private const int MaxMulticastRender = 16;
    private const int MallocSlot = 0x10;               // FMalloc::Malloc vtable byte offset (RE-UE4SS)
    private const int FreeSlot = 0x30;                 // FMalloc::Free vtable byte offset

    private const int PropIdenticalSlot = 0xB0;        // bool Identical(A, B, PortFlags)
    private const int PropGetValueTypeHashSlot = 0xF8; // uint32 GetValueTypeHashInternal(Src)
    private const int PropCopySingleToVmSlot = 0x100;  // void CopySingleValueToScriptVM(Dest, Src)
    private const int PropDestroyValueSlot = 0x128;    // void DestroyValueInternal(Dest)
    private const int PropInitializeValueSlot = 0x130; // void InitializeValueInternal(Dest)

    private readonly IMemory _memory;
    private readonly INameResolver _names;
    private readonly ObjectArrayView _objects;

    private readonly int _classPrivate;
    private readonly int _namePrivate;
    private readonly int _outerPrivate;
    private readonly int _objectFlags;
    private readonly int _superStruct;
    private readonly int _classDefaultObject;
    private readonly int _propertiesSize;

    private readonly int _childProperties;
    private readonly int _children;
    private readonly int _ufieldNext;
    private readonly int _fieldNext;
    private readonly int _fieldName;
    private readonly int _fieldClassPrivate;
    private readonly int _fieldClassName;
    private readonly int _fieldClassCastFlags;
    private readonly int _arrayDim;
    private readonly int _elementSize;
    private readonly int _propertyFlags;
    private readonly int _offsetInternal;
    private readonly int _propertyBaseSize;
    private readonly int _enumNames;
    private readonly int _processEventSlot;
    private readonly int _functionFlags;

    private readonly nint _classMeta;
    private readonly nint _functionMeta;
    private readonly nint _enumMeta;
    private readonly nint _scriptStructMeta;
    private readonly nint _fieldMeta;

    private readonly nint _moduleBase;
    private readonly long _moduleSize;
    private readonly nint _gmalloc;

    private (int Stride, int ValueOffset)? _namePointerMap;

    private nint _stringToTextTarget;
    private nint _stringToText;
    private bool _stringToTextResolved;

    private readonly ConcurrentDictionary<nint, Func<nint, UnrealContext, UObject>> _factories;
    private readonly ConcurrentDictionary<nint, Func<nint, UnrealContext, UObject>?> _factoryByClass;
    private readonly ConcurrentDictionary<Type, string> _wrapperNames;
    private readonly ConcurrentDictionary<string, Func<nint, UnrealContext, UObject>> _factoryNames;
    private readonly ConcurrentDictionary<string, Func<nint, UnrealContext, UStructValue>> _structFactories;
    private readonly ConcurrentDictionary<int, nint> _structsByName;
    private readonly ConcurrentDictionary<int, nint> _classesByName;

    internal int ProcessEventByteOffset =>
        _processEventSlot;

    internal IMemory Memory =>
        _memory;

    internal int ObjectCount =>
        _objects.Count;

    internal UnrealContext(IMemory memory, LayoutTable layout, INameResolver names, ObjectArrayView objects, nint gmalloc)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(objects);

        _factories = new ConcurrentDictionary<nint, Func<nint, UnrealContext, UObject>>();
        _factoryByClass = new ConcurrentDictionary<nint, Func<nint, UnrealContext, UObject>?>();
        _wrapperNames = new ConcurrentDictionary<Type, string>();
        _factoryNames = new ConcurrentDictionary<string, Func<nint, UnrealContext, UObject>>(StringComparer.Ordinal);
        _structFactories = new ConcurrentDictionary<string, Func<nint, UnrealContext, UStructValue>>(StringComparer.Ordinal);
        _structsByName = new ConcurrentDictionary<int, nint>();
        _classesByName = new ConcurrentDictionary<int, nint>();

        _memory = memory;
        _names = names;
        _objects = objects;
        _gmalloc = gmalloc;

        _classPrivate = layout.OffsetOrThrow(LayoutNames.ClassPrivate);
        _namePrivate = layout.OffsetOrThrow(LayoutNames.NamePrivate);
        _outerPrivate = layout.OffsetOrThrow(LayoutNames.OuterPrivate);
        _objectFlags = layout.OffsetOrThrow(LayoutNames.ObjectFlags);
        _superStruct = layout.OffsetOrThrow(LayoutNames.SuperStruct);
        _classDefaultObject = layout.OffsetOrThrow(LayoutNames.ClassDefaultObject);
        _propertiesSize = layout.OffsetOrThrow(LayoutNames.PropertiesSize);

        _childProperties = layout.OffsetOrThrow(LayoutNames.ChildProperties);
        _children = layout.OffsetOrThrow(LayoutNames.Children);
        _ufieldNext = layout.OffsetOrThrow(LayoutNames.FieldNext);
        _fieldNext = layout.OffsetOrThrow(LayoutNames.FieldChainNext);
        _fieldName = layout.OffsetOrThrow(LayoutNames.FieldNamePrivate);
        _fieldClassPrivate = layout.OffsetOrThrow(LayoutNames.FieldClassPrivate);
        _fieldClassName = layout.OffsetOrThrow(LayoutNames.FieldClassName);
        _fieldClassCastFlags = layout.OffsetOrThrow(LayoutNames.FieldClassCastFlags);
        _arrayDim = layout.OffsetOrThrow(LayoutNames.ArrayDim);
        _elementSize = layout.OffsetOrThrow(LayoutNames.ElementSize);
        _propertyFlags = layout.OffsetOrThrow(LayoutNames.PropertyFlags);
        _offsetInternal = layout.OffsetOrThrow(LayoutNames.OffsetInternal);
        _propertyBaseSize = layout.OffsetOrThrow(LayoutNames.PropertyBaseSize);
        _enumNames = layout.OffsetOrThrow(LayoutNames.EnumNames);
        _processEventSlot = layout.OffsetOrThrow(LayoutNames.ProcessEventSlot);
        _functionFlags = layout.OffsetOrThrow(LayoutNames.FunctionFlags);

        _classMeta = FindMetaClass();
        _functionMeta = FindClassNamed("Function");
        _enumMeta = FindClassNamed("Enum");
        _scriptStructMeta = FindClassNamed("ScriptStruct");
        _fieldMeta = FindClassNamed("Field");

        MainModule.TryGetRange(out _moduleBase, out _moduleSize);
    }

    internal bool TryFindNameId(string name, out int id)
    {
        return _names.TryFind(name, out id);
    }

    internal EFunctionFlags FunctionFlagsOf(nint function)
    {
        return _memory.TryRead(function + _functionFlags, out uint flags) ? (EFunctionFlags)flags : EFunctionFlags.None;
    }

    internal nint ClassVtable(nint classAddress)
    {
        var cdo = Pointer(classAddress, _classDefaultObject);

        return cdo is 0 ? 0 : Pointer(cdo, 0);
    }

    internal bool IsClassOrSubclass(nint klass, nint baseClass)
    {
        for (var depth = 0; klass is not 0 && depth < MaxSuperDepth; depth++)
        {
            if (klass == baseClass)
                return true;

            klass = Pointer(klass, _superStruct);
        }

        return false;
    }

    internal IEnumerable<nint> ClassVtablesWithAnyFunction(IReadOnlySet<int> nameIds, int from)
    {
        var seen = new HashSet<nint>();

        foreach (var address in _objects.AddressesFrom(from))
        {
            if (ClassPointerOf(address) != _classMeta)
                continue;

            for (var field = Pointer(address, _children); field is not 0; field = Pointer(field, _ufieldNext))
            {
                if (!nameIds.Contains(NameIdOf(field)))
                    continue;

                var vtable = ClassVtable(address);

                if (vtable is not 0 && seen.Add(vtable))
                    yield return vtable;

                break;
            }
        }
    }

    internal IEnumerable<nint> ClassVtablesWithFunction(int nameId)
    {
        var seen = new HashSet<nint>();

        foreach (var address in _objects.Addresses())
        {
            if (ClassPointerOf(address) != _classMeta)
                continue;

            for (var field = Pointer(address, _children); field is not 0; field = Pointer(field, _ufieldNext))
            {
                if (NameIdOf(field) == nameId)
                {
                    var vtable = ClassVtable(address);

                    if (vtable is not 0 && seen.Add(vtable))
                        yield return vtable;

                    break;
                }
            }
        }
    }

    internal int NameIdOf(nint address)
    {
        return _memory.TryRead(address + _namePrivate, out int id) ? id : 0;
    }

    internal string NameOf(nint address)
    {
        return NameAtSlot(address + _namePrivate);
    }

    internal nint ClassPointerOf(nint address)
    {
        return Pointer(address, _classPrivate);
    }

    internal nint OuterPointerOf(nint address)
    {
        return Pointer(address, _outerPrivate);
    }

    internal nint SuperPointerOf(nint address)
    {
        return Pointer(address, _superStruct);
    }

    internal nint DefaultObjectPointerOf(nint address)
    {
        return Pointer(address, _classDefaultObject);
    }

    private nint Pointer(nint address, int offset)
    {
        return _memory.TryRead(address + offset, out nint value) ? value : 0;
    }

    internal void RegisterSdkStruct(string structName, Func<nint, UnrealContext, UStructValue> factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(structName);
        ArgumentNullException.ThrowIfNull(factory);

        _structFactories[structName] = factory;
    }

    internal UStructValue WrapStruct(nint address, string? structName)
    {
        return structName is { } name && _structFactories.TryGetValue(name, out var factory)
            ? factory(address, this)
            : new UStructValue(address, this, structName);
    }

    internal void RegisterSdkFactory(string className, Type wrapper, Func<nint, UnrealContext, UObject> factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(className);
        ArgumentNullException.ThrowIfNull(wrapper);
        ArgumentNullException.ThrowIfNull(factory);

        _wrapperNames[wrapper] = className;
        _factoryNames[className] = factory;
    }

    internal string? EngineNameOf(Type wrapper)
    {
        return _wrapperNames.GetValueOrDefault(wrapper);
    }

    internal UObject Wrap(nint address)
    {
        var klass = ClassPointerOf(address);

        if (klass is not 0)
        {
            if (TypedFactory(klass) is { } factory)
                return factory(address, this);

            if (DerivesFrom(klass, _classMeta))
                return new UClass(address, this);

            if (DerivesFrom(klass, _functionMeta))
                return new UFunction(address, this);

            if (DerivesFrom(klass, _enumMeta))
                return new UEnum(address, this);

            if (DerivesFrom(klass, _scriptStructMeta))
                return new UScriptStruct(address, this);
        }

        return new UObject(address, this);
    }

    internal UObject? WrapOrNull(nint address)
    {
        return address is 0 ? null : Wrap(address);
    }

    internal UClass? AsClass(nint address)
    {
        return address is 0 ? null : new UClass(address, this);
    }

    internal bool IsReflectionClass(nint classPointer)
    {
        return _fieldMeta is not 0 && DerivesFrom(classPointer, _fieldMeta);
    }

    internal UStruct? AsStruct(nint address)
    {
        return address is 0 ? null : new UStruct(address, this);
    }

    internal bool IsA(nint address, nint type)
    {
        return type is not 0 && DerivesFrom(ClassPointerOf(address), type);
    }

    private Func<nint, UnrealContext, UObject>? TypedFactory(nint klass)
    {
        if (_factoryNames.IsEmpty)
            return null;

        if (_factoryByClass.TryGetValue(klass, out var cached))
            return cached;

        Func<nint, UnrealContext, UObject>? found = null;
        var current = klass;

        for (var depth = 0; current is not 0 && depth < MaxSuperDepth; current = SuperPointerOf(current), depth++)
        {
            if (_factories.TryGetValue(current, out var factory))
            {
                found = factory;
                break;
            }

            if (_factoryNames.TryGetValue(NameOf(current), out var late))
            {
                _factories[current] = late;
                found = late;
                break;
            }
        }

        _factoryByClass[klass] = found;
        return found;
    }

    private bool DerivesFrom(nint klass, nint target)
    {
        for (var depth = 0; klass is not 0 && depth < MaxSuperDepth; depth++)
        {
            if (klass == target)
                return true;

            klass = SuperPointerOf(klass);
        }

        return false;
    }

    internal UClass? FindClass(string name)
    {
        if (_classMeta is 0 || !_names.TryFind(name, out var id) || id is 0)
            return null;

        if (_classesByName.TryGetValue(id, out var cached) && NameIdOf(cached) == id && DerivesFrom(ClassPointerOf(cached), _classMeta))
            return new UClass(cached, this);

        foreach (var address in _objects.Addresses())
        {
            if (NameIdOf(address) == id && DerivesFrom(ClassPointerOf(address), _classMeta))
            {
                _classesByName[id] = address;
                return new UClass(address, this);
            }
        }

        return null;
    }

    internal UObject? FindObject(string name)
    {
        if (!_names.TryFind(name, out var id) || id is 0)
            return null;

        foreach (var address in _objects.Addresses())
        {
            if (NameIdOf(address) == id)
                return Wrap(address);
        }

        return null;
    }

    internal IEnumerable<UClass> AllClasses()
    {
        foreach (var address in _objects.Addresses())
        {
            if (ClassPointerOf(address) == _classMeta)
                yield return new UClass(address, this);
        }
    }

    internal IEnumerable<UClass> AllClassObjects()
    {
        foreach (var address in _objects.Addresses())
        {
            if (_classMeta is not 0 && DerivesFrom(ClassPointerOf(address), _classMeta))
                yield return new UClass(address, this);
        }
    }

    internal IEnumerable<UObject> ObjectsFrom(int start)
    {
        foreach (var address in _objects.AddressesFrom(start))
            yield return Wrap(address);
    }

    internal int StructSizeOf(UStructValue value)
    {
        return value.StructType is { } type ? PropertiesSizeOf(type.Address) : 0;
    }

    internal UScriptStruct? FindScriptStruct(string name)
    {
        if (_scriptStructMeta is 0 || !_names.TryFind(name, out var id) || id is 0)
            return null;

        if (_structsByName.TryGetValue(id, out var cached))
            return new UScriptStruct(cached, this);

        foreach (var address in _objects.Addresses())
        {
            if (NameIdOf(address) == id && ClassPointerOf(address) == _scriptStructMeta)
            {
                _structsByName[id] = address;

                return new UScriptStruct(address, this);
            }
        }

        return null;
    }

    internal IEnumerable<UScriptStruct> AllScriptStructs()
    {
        foreach (var address in _objects.Addresses())
        {
            if (_scriptStructMeta is not 0 && ClassPointerOf(address) == _scriptStructMeta)
                yield return new UScriptStruct(address, this);
        }
    }

    internal IEnumerable<UEnum> AllEnums()
    {
        foreach (var address in _objects.Addresses())
        {
            if (_enumMeta is not 0 && ClassPointerOf(address) == _enumMeta)
                yield return new UEnum(address, this);
        }
    }

    internal string PackageOf(nint address)
    {
        if (address is 0)
            return string.Empty;

        var current = address;

        for (var depth = 0; depth < MaxSuperDepth; depth++)
        {
            var outer = OuterPointerOf(current);

            if (outer is 0)
                break;

            current = outer;
        }

        return NameOf(current);
    }

    internal IReadOnlyList<(UObject Instance, FProperty Property)> FindLiveExamples(params PropertyKind[] kinds)
    {
        var all = new HashSet<PropertyKind>(kinds);
        var wanted = new HashSet<PropertyKind>(kinds);
        var best = new Dictionary<PropertyKind, (UObject, FProperty)>();
        var fallback = new Dictionary<PropertyKind, (UObject, FProperty)>();
        var byClass = new Dictionary<nint, List<FProperty>>();
        var scanned = 0;

        foreach (var address in _objects.Addresses())
        {
            if (wanted.Count is 0 || scanned++ > MaxExampleScan)
                break;

            if ((ObjectFlagsOf(address) & EObjectFlags.ClassDefaultObject) is not 0)
                continue;

            var classPointer = ClassPointerOf(address);

            if (classPointer is 0)
                continue;

            if (!byClass.TryGetValue(classPointer, out var properties))
            {
                properties = [];

                foreach (var property in new UClass(classPointer, this).AllProperties)
                {
                    if (all.Contains(property.Kind))
                        properties.Add(property);
                }

                byClass[classPointer] = properties;
            }

            if (properties.Count is 0)
                continue;

            var instance = Wrap(address);

            foreach (var property in properties)
            {
                if (!wanted.Contains(property.Kind))
                    continue;

                fallback.TryAdd(property.Kind, (instance, property));

                if (!IsTrivialValue(property.FormatValue(instance.Address)))
                {
                    best[property.Kind] = (instance, property);
                    wanted.Remove(property.Kind);
                }
            }
        }

        var found = new List<(UObject, FProperty)>();

        foreach (var kind in kinds)
        {
            if (best.TryGetValue(kind, out var example) || fallback.TryGetValue(kind, out example))
                found.Add(example);
        }

        return found;
    }

    private static bool IsTrivialValue(string value)
    {
        return value is "" or "\"\"" or "None" or "[]" or "{}" or "0" or "null" or "false" or "<sparse: unbound>";
    }

    internal (UObject Instance, FProperty Property)? FindLiveExample(Func<UObject, FProperty, bool> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var byClass = new Dictionary<nint, List<FProperty>>();
        var scanned = 0;

        foreach (var address in _objects.Addresses())
        {
            if (scanned++ > MaxExampleScan)
                break;

            if ((ObjectFlagsOf(address) & EObjectFlags.ClassDefaultObject) is not 0)
                continue;

            var classPointer = ClassPointerOf(address);

            if (classPointer is 0)
                continue;

            if (!byClass.TryGetValue(classPointer, out var properties))
            {
                properties = [.. new UClass(classPointer, this).AllProperties];
                byClass[classPointer] = properties;
            }

            if (properties.Count is 0)
                continue;

            var instance = Wrap(address);

            foreach (var property in properties)
            {
                if (match(instance, property))
                    return (instance, property);
            }
        }

        return null;
    }

    internal UObject? FindFirstOf(string className)
    {
        foreach (var instance in FindAllOf(className))
            return instance;

        return null;
    }

    internal IEnumerable<UObject> FindAllOf(string className)
    {
        if (FindClass(className) is not { } klass)
            yield break;

        var target = klass.Address;
        var matches = new Dictionary<nint, bool>();

        foreach (var address in _objects.Addresses())
        {
            if ((ObjectFlagsOf(address) & EObjectFlags.ClassDefaultObject) is not 0)
                continue;

            var classPointer = ClassPointerOf(address);

            if (classPointer is 0)
                continue;

            if (!matches.TryGetValue(classPointer, out var derives))
            {
                derives = DerivesFrom(classPointer, target);
                matches[classPointer] = derives;
            }

            if (derives)
                yield return Wrap(address);
        }
    }

    internal UObject? GameWorld()
    {
        var worldClass = FindClass("World");

        if (worldClass is null)
            return null;

        UObject? fallback = null;

        foreach (var address in _objects.Addresses())
        {
            if ((ObjectFlagsOf(address) & EObjectFlags.ClassDefaultObject) is not 0 || !IsA(address, worldClass.Address))
                continue;

            var world = Wrap(address);

            if (world.TryGet<nint>("OwningGameInstance", out var gameInstance) && gameInstance is not 0)
                return world;

            fallback ??= world;
        }

        return fallback;
    }

    internal UObject? SpawnActor(nint actorClass, nint owner, ReadOnlySpan<byte> transform)
    {
        return SpawnActor(actorClass, owner, transform.IsEmpty ? null : transform.ToArray());
    }

    internal UObject? SpawnActor(nint actorClass, nint owner)
    {
        return SpawnActor(actorClass, owner, null);
    }

    private UObject? SpawnActor(nint actorClass, nint owner, byte[]? placement)
    {
        if (actorClass is 0
            || GameWorld() is not { } world
            || FindClass("GameplayStatics") is not { ClassDefaultObject: { } library } gameplayStatics
            || gameplayStatics.FindFunction("BeginDeferredActorSpawnFromClass") is not { } begin
            || gameplayStatics.FindFunction("FinishSpawningActor") is not { } finish)
            return null;

        var transform = Array.Empty<byte>();

        foreach (var parameter in begin.Parameters)
        {
            if (parameter is FStructProperty transformParameter)
            {
                transform = placement is { Length: > 0 } && placement.Length == transformParameter.ElementSize
                    ? placement
                    : IdentityTransform(transformParameter);

                break;
            }
        }

        if (InvokeFunction(library.Address, begin.Address, SpawnArguments(begin, world.Address, actorClass, owner, 0, transform)) is not { Length: >= 8 } beginResult)
            return null;

        var actor = (nint)BitConverter.ToInt64(beginResult, 0);

        if (actor is 0)
            return null;

        InvokeFunction(library.Address, finish.Address, SpawnArguments(finish, world.Address, actorClass, owner, actor, transform));

        return WrapOrNull(actor);
    }

    internal UObject? SpawnObject(nint objectClass, nint outer)
    {
        if (objectClass is 0
            || FindClass("GameplayStatics") is not { ClassDefaultObject: { } library } gameplayStatics
            || gameplayStatics.FindFunction("SpawnObject") is not { } function)
            return null;

        if (outer is 0)
            outer = GameWorld()?.Address ?? 0;

        if (InvokeFunction(library.Address, function.Address, SpawnArguments(function, 0, objectClass, outer, 0, Array.Empty<byte>())) is not { Length: >= 8 } result)
            return null;

        var spawned = (nint)BitConverter.ToInt64(result, 0);

        return spawned is not 0 ? WrapOrNull(spawned) : null;
    }

    private List<byte[]> SpawnArguments(UFunction function, nint world, nint objectClass, nint owner, nint self, byte[] transform)
    {
        var arguments = new List<byte[]>();

        foreach (var parameter in function.Parameters)
        {
            arguments.Add(parameter switch
            {
                FStructProperty => transform,
                { Kind: PropertyKind.Class } => Pointer(objectClass),
                { Kind: PropertyKind.Enum } => FirstByte(1, parameter.ElementSize),
                { Kind: PropertyKind.Object } when parameter.Name.Contains("World", StringComparison.OrdinalIgnoreCase) => Pointer(world),
                { Kind: PropertyKind.Object } when parameter.Name.Contains("Owner", StringComparison.OrdinalIgnoreCase) => Pointer(owner),
                { Kind: PropertyKind.Object } when parameter.Name.Contains("Outer", StringComparison.OrdinalIgnoreCase) => Pointer(owner),
                { Kind: PropertyKind.Object } when parameter.Name.Contains("Actor", StringComparison.OrdinalIgnoreCase) => Pointer(self),
                _ => new byte[parameter.ElementSize * Math.Max(1, ArrayDimOf(parameter.Address))]
            });
        }

        return arguments;
    }

    private static byte[] Pointer(nint value)
    {
        return BitConverter.GetBytes(value);
    }

    internal bool DestroyActor(UObject actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return actor.Class?.FindFunction("K2_DestroyActor") is { } destroy && InvokeFunction(actor.Address, destroy.Address, []) is not null;
    }

    private static byte[] FirstByte(byte value, int size)
    {
        var bytes = new byte[Math.Max(1, size)];
        bytes[0] = value;

        return bytes;
    }

    private static byte[] IdentityTransform(FStructProperty transformParameter)
    {
        var bytes = new byte[Math.Max(0, transformParameter.ElementSize)];

        if (transformParameter.Struct is not { } transform)
            return bytes;

        if (transform.FindProperty("Rotation") is FStructProperty rotation && rotation.Struct?.FindProperty("W") is { } w)
            WriteDouble(bytes, rotation.Offset + w.Offset, 1.0);

        if (transform.FindProperty("Scale3D") is FStructProperty scale && scale.Struct is { } vector)
        {
            foreach (var axis in new[] { "X", "Y", "Z" })
            {
                if (vector.FindProperty(axis) is { } component)
                    WriteDouble(bytes, scale.Offset + component.Offset, 1.0);
            }
        }

        return bytes;
    }

    private static void WriteDouble(byte[] buffer, int offset, double value)
    {
        if (offset >= 0 && offset + sizeof(double) <= buffer.Length)
            BitConverter.GetBytes(value).CopyTo(buffer, offset);
    }

    internal EObjectFlags ObjectFlagsOf(nint address)
    {
        return _memory.TryRead(address + _objectFlags, out uint flags) ? (EObjectFlags)flags : EObjectFlags.None;
    }

    internal string FieldNameOf(nint field)
    {
        return _memory.TryRead(field + _fieldName, out int id) && _names.TryResolve(id, out var name) ? name : string.Empty;
    }

    internal nint FieldNextOf(nint field)
    {
        return Pointer(field, _fieldNext);
    }

    internal nint FieldClassOf(nint field)
    {
        return Pointer(field, _fieldClassPrivate);
    }

    internal string FieldClassNameOf(nint fieldClass)
    {
        return _memory.TryRead(fieldClass + _fieldClassName, out int id) && _names.TryResolve(id, out var name) ? name : string.Empty;
    }

    internal EClassCastFlags FieldClassCastFlagsOf(nint fieldClass)
    {
        return _memory.TryRead(fieldClass + _fieldClassCastFlags, out ulong flags) ? (EClassCastFlags)flags : EClassCastFlags.None;
    }

    internal int ArrayDimOf(nint property)
    {
        return _memory.TryRead(property + _arrayDim, out int value) ? value : 0;
    }

    internal int ElementSizeOf(nint property)
    {
        return _memory.TryRead(property + _elementSize, out int value) ? value : 0;
    }

    internal EPropertyFlags PropertyFlagsOf(nint property)
    {
        return _memory.TryRead(property + _propertyFlags, out ulong flags) ? (EPropertyFlags)flags : EPropertyFlags.None;
    }

    internal int OffsetInternalOf(nint property)
    {
        return _memory.TryRead(property + _offsetInternal, out int value) ? value : 0;
    }

    internal PropertyKind ClassifyProperty(nint property)
    {
        return PropertyKinds.FromCastFlags(FieldClassCastFlagsOf(FieldClassOf(property)));
    }

    internal FField? WrapField(nint address)
    {
        return address is 0 ? null : WrapPropertyTyped(address);
    }

    internal FProperty? WrapProperty(nint address)
    {
        return address is 0 ? null : WrapPropertyTyped(address);
    }

    internal FFieldClass? WrapFieldClass(nint address)
    {
        return address is 0 ? null : new FFieldClass(address, this);
    }

    private FProperty WrapPropertyTyped(nint address)
    {
        return ClassifyProperty(address) switch
        {
            PropertyKind.Bool => new FBoolProperty(address, this),
            PropertyKind.Object or PropertyKind.Class => new FObjectProperty(address, this),
            PropertyKind.Name => new FNameProperty(address, this),
            PropertyKind.Str => new FStrProperty(address, this),
            PropertyKind.Text => new FTextProperty(address, this),
            PropertyKind.Byte => new FByteProperty(address, this),
            PropertyKind.Enum => new FEnumProperty(address, this),
            PropertyKind.Struct => new FStructProperty(address, this),
            PropertyKind.Array => new FArrayProperty(address, this),
            PropertyKind.Map => new FMapProperty(address, this),
            PropertyKind.Set => new FSetProperty(address, this),
            PropertyKind.SoftObject or PropertyKind.SoftClass => new FSoftObjectProperty(address, this),
            PropertyKind.WeakObject => new FWeakObjectProperty(address, this),
            PropertyKind.LazyObject => new FLazyObjectProperty(address, this),
            PropertyKind.Interface => new FInterfaceProperty(address, this),
            PropertyKind.Delegate => new FDelegateProperty(address, this),
            PropertyKind.MulticastInlineDelegate => new FMulticastInlineDelegateProperty(address, this),
            PropertyKind.MulticastSparseDelegate => new FMulticastSparseDelegateProperty(address, this),
            _ => new FProperty(address, this)
        };
    }

    internal byte BoolByteOffsetOf(nint property)
    {
        return _memory.TryRead(property + _propertyBaseSize + 1, out byte value) ? value : (byte)0;
    }

    internal byte BoolFieldMaskOf(nint property)
    {
        return _memory.TryRead(property + _propertyBaseSize + 3, out byte value) ? value : (byte)0xFF;
    }

    internal bool IsReadable(nint address, int length)
    {
        return _memory.IsReadable(address, length);
    }

    internal UObject? ObjectByIndex(int index, int expectedSerial)
    {
        if (index < 0 || !_objects.TryGetAddress(index, out var address) || address is 0)
            return null;

        if (_objects.TryGetSerial(index, out var actual) && actual != expectedSerial)
            return null;

        return IsReadable(address, 0x20) ? Wrap(address) : null;
    }

    internal string ResolveName(int comparisonIndex)
    {
        return _names.TryResolve(comparisonIndex, out var name) ? name : string.Empty;
    }

    internal string NameAtSlot(nint address)
    {
        if (!_memory.TryRead(address, out int comparisonIndex))
            return string.Empty;

        var number = _memory.TryRead(address + sizeof(int), out int value) ? value : 0;

        return _names.TryResolve(comparisonIndex, number, out var name) ? name : string.Empty;
    }

    internal string SoftObjectPath(nint value)
    {
        var package = NameAtSlot(value + 0x10);

        if (package.Length is 0 || package == "None")
            return string.Empty;

        var asset = NameAtSlot(value + 0x18);
        var path = asset.Length is 0 ? package : $"{package}.{asset}";
        var sub = ReadFString(value + 0x20);

        return sub.Length is 0 ? path : $"{path}:{sub}";
    }

    internal UObject? WeakObject(nint value)
    {
        return _memory.TryRead(value, out int index) && _memory.TryRead(value + sizeof(int), out int serial) && serial > 0 ? ObjectByIndex(index, serial) : null;
    }

    internal bool WriteWeakObject(nint value, nint target)
    {
        var bytes = new byte[2 * sizeof(int)];

        if (target is not 0)
        {
            if (!TryWeakPointer(target, out var index, out var serial) || serial <= 0)
                return false;

            BitConverter.GetBytes(index).CopyTo(bytes, 0);
            BitConverter.GetBytes(serial).CopyTo(bytes, sizeof(int));
        }

        return WriteBytes(value, bytes);
    }

    internal bool WriteSoftObjectPath(nint value, string path)
    {
        var sub = string.Empty;
        var separator = path.IndexOf(':', StringComparison.Ordinal);

        if (separator >= 0)
        {
            sub = path[(separator + 1)..];
            path = path[..separator];
        }

        var dot = path.LastIndexOf('.');
        var package = dot >= 0 ? path[..dot] : path;
        var asset = dot >= 0 ? path[(dot + 1)..] : string.Empty;
        var empty = new byte[sizeof(long)];

        return WriteBytes(value, new byte[0x10])
            && WriteBytes(value + 0x10, package.Length is 0 ? empty : NameBytes(package))
            && WriteBytes(value + 0x18, asset.Length is 0 ? empty : NameBytes(asset))
            && WriteFString(value + 0x20, sub);
    }

    internal UObject? LoadAsset(string path, bool asClass)
    {
        GameThread.EnsureCurrent("Loading an asset");

        if (FindClass("KismetSystemLibrary") is not { ClassDefaultObject: { } library } kismet
            || kismet.FindFunction(asClass ? "LoadClassAsset_Blocking" : "LoadAsset_Blocking") is not { } function
            || function.Parameters.FirstOrDefault() is not { Kind: PropertyKind.SoftObject or PropertyKind.SoftClass } parameter)
            return null;

        var size = parameter.ElementSize;
        var argument = Malloc((nuint)size, 0);

        if (argument is 0)
            return null;

        try
        {
            if (!WriteBytes(argument, new byte[size]) || !WriteSoftObjectPath(argument, path))
                return null;

            if (InvokeFunction(library.Address, function.Address, [BytesAt(argument, size)]) is not { Length: >= 8 } result)
                return null;

            return WrapOrNull((nint)BitConverter.ToInt64(result, 0));
        }
        finally
        {
            PropertyDestroy(parameter.Address, argument);
            Free(argument);
        }
    }

    internal IEnumerable<KeyValuePair<string, UStructValue>> DataTableRows(nint table)
    {
        if (RowMapOffsetOf(table) is not { } offset || NamePointerMapLayout() is not var (stride, valueOffset))
            yield break;

        var rowStruct = DataTableRowType(table)?.Name;

        foreach (var element in SparseElements(table + offset, stride))
        {
            if (!_memory.TryRead(element + valueOffset, out nint row) || row is 0)
                continue;

            yield return new KeyValuePair<string, UStructValue>(NameAtSlot(element), WrapStruct(row, rowStruct));
        }
    }

    internal UScriptStruct? DataTableRowType(nint table)
    {
        return (AsClass(ClassPointerOf(table))?.FindProperty("RowStruct") as FObjectProperty)?.GetObjectAt(table) is { } rowStruct
            ? WrapScriptStruct(rowStruct.Address)
            : null;
    }

    private int? RowMapOffsetOf(nint table)
    {
        if (AsClass(ClassPointerOf(table)) is not { } klass || klass.FindProperty("RowStruct") is not { } rowStruct)
            return null;

        var start = rowStruct.Offset + rowStruct.ElementSize;
        var next = int.MaxValue;

        foreach (var property in klass.AllProperties)
        {
            if (property.Offset > rowStruct.Offset && property.Offset < next)
                next = property.Offset;
        }

        return next - start == ScriptMapSize ? start : null;
    }

    private (int Stride, int ValueOffset)? NamePointerMapLayout()
    {
        if (_namePointerMap is { } cached)
            return cached;

        foreach (var klass in AllClasses())
        {
            foreach (var property in klass.Properties)
            {
                if (property is not FMapProperty { Key: FNameProperty, Value: { } value } map || value.ElementSize != nint.Size)
                    continue;

                var layout = MapLayoutOf(map.Address);

                if (layout.SlotStride > 0)
                    return _namePointerMap = (layout.SlotStride, layout.ValueOffset);
            }
        }

        return null;
    }

    internal string LazyGuid(nint value)
    {
        var guid = value + 0x10;

        if (!_memory.TryRead(guid, out uint a) || !_memory.TryRead(guid + sizeof(uint), out uint b) || !_memory.TryRead(guid + 2 * sizeof(uint), out uint c) || !_memory.TryRead(guid + 3 * sizeof(uint), out uint d))
            return string.Empty;

        return (a | b | c | d) is 0 ? string.Empty : $"{a:X8}{b:X8}{c:X8}{d:X8}";
    }

    internal string ScriptDelegate(nint address)
    {
        if (!_memory.TryRead(address, out int index) || !_memory.TryRead(address + sizeof(int), out int serial) || (index <= 0 && serial <= 0))
            return "None";

        var bound = ObjectByIndex(index, serial);

        if (bound is null)
            return "None";

        var function = NameAtSlot(address + 2 * sizeof(int));

        return function.Length is 0 || function == "None" ? bound.Name : $"{bound.Name}.{function}";
    }

    internal bool TryWeakPointer(nint address, out int index, out int serial)
    {
        index = 0;
        serial = 0;

        if (address is 0 || !_memory.TryRead(address + _objects.InternalIndexOffset, out index) || index < 0 || index >= _objects.Count)
            return false;

        _objects.TryGetSerial(index, out serial);

        return true;
    }

    internal byte[] ScriptDelegateBytes(nint target, string functionName)
    {
        var bytes = new byte[ScriptDelegateStride];

        if (TryWeakPointer(target, out var index, out var serial))
        {
            BitConverter.GetBytes(index).CopyTo(bytes, 0);
            BitConverter.GetBytes(serial).CopyTo(bytes, sizeof(int));
        }

        NameBytes(functionName).CopyTo(bytes, 2 * sizeof(int));

        return bytes;
    }

    internal bool BindDelegate(nint slot, nint target, string functionName)
    {
        return target is not 0 && WriteBytes(slot, ScriptDelegateBytes(target, functionName));
    }

    internal bool ClearDelegate(nint slot)
    {
        return WriteBytes(slot, new byte[ScriptDelegateStride]);
    }

    internal bool MulticastAdd(nint header, nint target, string functionName)
    {
        if (target is 0 || header is 0)
            return false;

        if (MulticastFind(header, target, functionName) >= 0)
            return true;

        return TryReadArrayHeader(header, out _, out var num, out _) && ArrayInsert(header, 0, ScriptDelegateStride, num, ScriptDelegateBytes(target, functionName)) >= 0;
    }

    internal bool MulticastRemove(nint header, nint target, string functionName)
    {
        var index = header is not 0 ? MulticastFind(header, target, functionName) : -1;

        return index >= 0 && ArrayRemoveAt(header, 0, ScriptDelegateStride, index, 1);
    }

    internal bool MulticastClear(nint header)
    {
        return header is not 0 && ArrayClear(header, 0, ScriptDelegateStride);
    }

    internal int MulticastFind(nint header, nint target, string functionName)
    {
        if (!TryReadArray(header, out var data, out var count) || !TryWeakPointer(target, out var targetIndex, out _) || !_names.TryFind(functionName, out var functionId))
            return -1;

        for (var i = 0; i < count; i++)
        {
            var element = data + (nint)i * ScriptDelegateStride;

            if (_memory.TryRead(element, out int boundIndex) && boundIndex == targetIndex && _memory.TryRead(element + 2 * sizeof(int), out int boundFunction) && boundFunction == functionId)
                return i;
        }

        return -1;
    }

    internal int BroadcastDelegate(nint slot, IReadOnlyList<byte[]> args)
    {
        return InvokeDelegateBinding(slot, args) ? 1 : 0;
    }

    internal int BroadcastMulticast(nint header, IReadOnlyList<byte[]> args)
    {
        if (!TryReadArray(header, out var data, out var count))
            return 0;

        var fired = 0;

        for (var i = 0; i < count; i++)
        {
            if (InvokeDelegateBinding(data + (nint)i * ScriptDelegateStride, args))
                fired++;
        }

        return fired;
    }

    private bool InvokeDelegateBinding(nint delegateAddress, IReadOnlyList<byte[]> args)
    {
        if (!_memory.TryRead(delegateAddress, out int index) || !_memory.TryRead(delegateAddress + sizeof(int), out int serial) || serial <= 0)
            return false;

        var bound = ObjectByIndex(index, serial);
        var function = NameAtSlot(delegateAddress + 2 * sizeof(int));

        return bound is not null && function.Length is not 0 && bound.Class?.FindFunction(function) is { } target && InvokeFunction(bound.Address, target.Address, args) is not null;
    }

    internal string ReadFString(nint address)
    {
        if (!_memory.TryRead(address, out nint data) || data is 0)
            return string.Empty;

        if (!_memory.TryRead(address + nint.Size, out int num) || num <= 1 || num > MaxStringChars)
            return string.Empty;

        var bytes = (num - 1) * sizeof(char);
        var buffer = new byte[bytes];

        return _memory.TryRead(data, buffer) ? Encoding.Unicode.GetString(buffer) : string.Empty;
    }

    internal string TextValue(nint textAddress)
    {
        if (!_memory.TryRead(textAddress, out nint data) || data is 0 || !_memory.IsReadable(data, nint.Size))
            return string.Empty;

        if (!_memory.TryRead(data, out nint vtable) || !InMainModule(vtable))
            return string.Empty;

        if (!_memory.TryRead(vtable + TextDisplayStringSlot, out nint getDisplayString) || !InMainModule(getDisplayString))
            return string.Empty;

        var display = InvokeDisplayString(getDisplayString, data);

        return display is 0 ? string.Empty : ReadFString(display);
    }

    internal bool WriteText(nint textProperty, nint slot, string value)
    {
        if (!TryResolveStringToText(out var target, out var function))
            return false;

        if (InvokeFunction(target, function, [Encoding.Unicode.GetBytes(value)]) is not { Length: > 0 } text)
            return false;

        return PropertyDestroy(textProperty, slot) && WriteBytes(slot, text);
    }

    private bool TryResolveStringToText(out nint target, out nint function)
    {
        if (!_stringToTextResolved)
        {
            _stringToTextResolved = true;

            var library = FindClass("KismetTextLibrary");
            _stringToTextTarget = library?.ClassDefaultObject?.Address ?? 0;
            _stringToText = library?.FindFunction("Conv_StringToText")?.Address ?? 0;
        }

        target = _stringToTextTarget;
        function = _stringToText;

        return target is not 0 && function is not 0;
    }

    private bool InMainModule(nint address)
    {
        return _moduleSize > 0 && address >= _moduleBase && address < _moduleBase + _moduleSize;
    }

    private static unsafe nint InvokeDisplayString(nint function, nint self)
    {
        return ((delegate* unmanaged<nint, nint>)function)(self);
    }

    internal string MulticastList(nint listAddress)
    {
        if (!TryReadArray(listAddress, out var data, out var count))
            return "[]";

        var builder = new StringBuilder("[");
        var shown = Math.Min(count, MaxMulticastRender);

        for (var index = 0; index < shown; index++)
        {
            if (index > 0)
                builder.Append(", ");

            builder.Append(ScriptDelegate(data + index * ScriptDelegateStride));
        }

        if (count > shown)
            builder.Append(", …");

        return builder.Append(']').ToString();
    }

    internal nint MulticastDelegateOf(nint property, nint value)
    {
        if (!_memory.TryRead(property, out nint vtable) || !InMainModule(vtable))
            return 0;

        if (!_memory.TryRead(vtable + MulticastGetDelegateSlot, out nint getMulticastDelegate) || !InMainModule(getMulticastDelegate))
            return 0;

        return InvokeGetMulticastDelegate(getMulticastDelegate, property, value);
    }

    private static unsafe nint InvokeGetMulticastDelegate(nint function, nint self, nint value)
    {
        return ((delegate* unmanaged<nint, nint, nint>)function)(self, value);
    }

    internal bool PropertyHash(nint property, nint valuePtr, out uint hash)
    {
        hash = 0;

        if ((PropertyFlagsOf(property) & EPropertyFlags.HasGetValueTypeHash) is 0 || !TryResolvePropertyVirtual(property, PropGetValueTypeHashSlot, out var function))
            return false;

        hash = InvokePropertyHash(function, property, valuePtr);

        return true;
    }

    internal bool PropertyIdentical(nint property, nint a, nint b)
    {
        return TryResolvePropertyVirtual(property, PropIdenticalSlot, out var function) && InvokePropertyIdentical(function, property, a, b, 0);
    }

    internal int PropertiesSizeOf(nint structType)
    {
        return _memory.TryRead(structType + _propertiesSize, out int value) ? value : 0;
    }

    internal nint AllocateStruct(nint structType)
    {
        var size = PropertiesSizeOf(structType);

        if (size <= 0)
            return 0;

        var address = Malloc((nuint)size, 0);

        if (address is 0)
            return 0;

        if (!WriteBytes(address, new byte[size]))
        {
            Free(address);

            return 0;
        }

        foreach (var member in PropertiesOf(structType))
            PropertyInitialize(member.Address, address + member.Offset);

        return address;
    }

    internal void DestroyStruct(nint structType, nint address)
    {
        if (address is 0)
            return;

        foreach (var member in PropertiesOf(structType))
            PropertyDestroy(member.Address, address + member.Offset);

        Free(address);
    }

    internal bool PropertyInitialize(nint property, nint dest)
    {
        if ((PropertyFlagsOf(property) & EPropertyFlags.ZeroConstructor) is not 0)
            return ZeroFill(dest, ElementSizeOf(property));

        if (!TryResolvePropertyVirtual(property, PropInitializeValueSlot, out var function))
            return false;

        InvokePropertyUnary(function, property, dest);

        return true;
    }

    internal bool PropertyDestroy(nint property, nint dest)
    {
        if ((PropertyFlagsOf(property) & EPropertyFlags.NoDestructor) is not 0)
            return true;

        if (!TryResolvePropertyVirtual(property, PropDestroyValueSlot, out var function))
            return false;

        InvokePropertyUnary(function, property, dest);

        return true;
    }

    internal bool PropertyCopySingle(nint property, nint dest, nint src)
    {
        if (!TryResolvePropertyVirtual(property, PropCopySingleToVmSlot, out var function))
            return false;

        InvokePropertyCopy(function, property, dest, src);

        return true;
    }

    internal bool PropertyIsPlainOldData(nint property)
    {
        return (PropertyFlagsOf(property) & EPropertyFlags.IsPlainOldData) is not 0;
    }

    internal bool PropertyConstructFrom(nint property, nint dest, ReadOnlySpan<byte> sourceBytes)
    {
        if (PropertyIsPlainOldData(property))
            return WriteBytes(dest, sourceBytes);

        return PropertyInitialize(property, dest) && CopyFromScratch(property, dest, sourceBytes);
    }

    internal bool PropertyAssignFrom(nint property, nint dest, ReadOnlySpan<byte> sourceBytes)
    {
        return PropertyIsPlainOldData(property) ? WriteBytes(dest, sourceBytes) : CopyFromScratch(property, dest, sourceBytes);
    }

    private bool CopyFromScratch(nint property, nint dest, ReadOnlySpan<byte> sourceBytes)
    {
        var scratch = Malloc((nuint)sourceBytes.Length, 0);

        if (scratch is 0)
            return false;

        try
        {
            return WriteBytes(scratch, sourceBytes) && PropertyCopySingle(property, dest, scratch);
        }
        finally
        {
            Free(scratch);
        }
    }

    internal byte[]? InvokeFunction(nint target, nint function, IReadOnlyList<byte[]> args)
    {
        return InvokeFunction(target, function, args, null, out _);
    }

    internal byte[]? InvokeFunction(nint target, nint function, IReadOnlyList<byte[]> args, out byte[][] outputs)
    {
        return InvokeFunction(target, function, args, null, out outputs);
    }

    internal byte[]? InvokeFunction(nint target, nint function, IReadOnlyList<byte[]> args, IReadOnlyList<nint>? destinations, out byte[][] outputs)
    {
        return InvokeFunction(target, function, args, destinations, 0, out outputs);
    }

    internal byte[]? InvokeFunction(nint target, nint function, IReadOnlyList<byte[]> args, IReadOnlyList<nint>? destinations, nint returnDestination, out byte[][] outputs)
    {
        GameThread.EnsureCurrent("Calling a game function");

        outputs = [];

        if (target is 0 || function is 0 || !TryResolveProcessEvent(target, out var processEvent))
            return null;

        var inputs = new List<nint>();
        var returnParm = (nint)0;
        var frameSize = 0;

        foreach (var property in PropertiesOf(function))
        {
            var flags = PropertyFlagsOf(property.Address);

            if ((flags & EPropertyFlags.Parm) is 0)
                continue;

            frameSize = Math.Max(frameSize, OffsetInternalOf(property.Address) + ElementSizeOf(property.Address) * Math.Max(1, ArrayDimOf(property.Address)));

            if ((flags & EPropertyFlags.ReturnParm) is not 0)
                returnParm = property.Address;
            else
                inputs.Add(property.Address);
        }

        if (args.Count != inputs.Count || frameSize > MaxFrameSize)
            return null;

        if (frameSize is 0)
        {
            InvokeProcessEvent(processEvent, target, function, 0);

            return [];
        }

        var frame = Malloc((nuint)frameSize, 0);

        if (frame is 0)
            return null;

        try
        {
            if (!WriteBytes(frame, new byte[frameSize]))
                return null;

            for (var i = 0; i < inputs.Count; i++)
            {
                var slot = frame + OffsetInternalOf(inputs[i]);

                if (WrapProperty(inputs[i]) is FStrProperty)
                {
                    if (!BuildStringArgument(slot, args[i]))
                        return null;

                    continue;
                }

                var expected = ElementSizeOf(inputs[i]) * Math.Max(1, ArrayDimOf(inputs[i]));

                if (args[i].Length != expected || !PropertyConstructFrom(inputs[i], slot, args[i]))
                    return null;
            }

            InvokeProcessEvent(processEvent, target, function, frame);

            var result = returnParm is not 0 ? Capture(returnParm, frame + OffsetInternalOf(returnParm)) : Array.Empty<byte>();
            var captured = new byte[inputs.Count][];

            for (var i = 0; i < inputs.Count; i++)
                captured[i] = Capture(inputs[i], frame + OffsetInternalOf(inputs[i]));

            for (var i = 0; destinations is not null && i < inputs.Count && i < destinations.Count; i++)
            {
                if (destinations[i] is not 0)
                    PropertyAssignFrom(inputs[i], destinations[i], captured[i]);
            }

            foreach (var input in inputs)
                PropertyDestroy(input, frame + OffsetInternalOf(input));

            if (returnParm is not 0 && returnDestination is not 0)
            {
                PropertyAssignFrom(returnParm, returnDestination, result);
                PropertyDestroy(returnParm, frame + OffsetInternalOf(returnParm));
            }
            else if (returnParm is not 0 && WrapProperty(returnParm) is FStrProperty)
            {
                PropertyDestroy(returnParm, frame + OffsetInternalOf(returnParm));
            }
            else if (returnParm is not 0 && WrapProperty(returnParm) is FStructProperty)
            {
                PropertyDestroy(returnParm, frame + OffsetInternalOf(returnParm));
                result = [];
            }

            outputs = captured;
            return result;
        }
        finally
        {
            Free(frame);
        }
    }

    private byte[] Capture(nint property, nint slot)
    {
        switch (WrapProperty(property))
        {
            case FStrProperty:
                return Encoding.Unicode.GetBytes(ReadFString(slot));

            case FNameProperty:
                return Encoding.Unicode.GetBytes(NameAt(slot));

            case FArrayProperty { Inner: { } inner } when IsFlatElement(inner):
                return CaptureElements(slot, inner.ElementSize);
        }

        var bytes = new byte[ElementSizeOf(property) * Math.Max(1, ArrayDimOf(property))];

        ReadBytes(slot, bytes);

        return bytes;
    }

    private static bool IsFlatElement(FProperty inner)
    {
        return inner.Kind is PropertyKind.Object or PropertyKind.Class or PropertyKind.WeakObject or PropertyKind.Enum or PropertyKind.Bool or PropertyKind.Byte
            or PropertyKind.Int8 or PropertyKind.Int16 or PropertyKind.Int32 or PropertyKind.Int64
            or PropertyKind.UInt16 or PropertyKind.UInt32 or PropertyKind.UInt64
            or PropertyKind.Float or PropertyKind.Double or PropertyKind.Name;
    }

    private byte[] CaptureElements(nint slot, int stride)
    {
        if (stride <= 0 || !TryReadArray(slot, out var data, out var count) || count <= 0)
            return [];

        var bytes = new byte[count * stride];

        return ReadBytes(data, bytes) ? bytes : [];
    }

    private bool TryResolveProcessEvent(nint target, out nint function)
    {
        function = 0;

        return _memory.TryRead(target, out nint vtable) && InMainModule(vtable)
            && _memory.TryRead(vtable + _processEventSlot, out function) && function is not 0;
    }

    private static unsafe void InvokeProcessEvent(nint function, nint self, nint uFunction, nint parms)
    {
        ((delegate* unmanaged<nint, nint, nint, void>)function)(self, uFunction, parms);
    }

    private bool TryResolvePropertyVirtual(nint property, int slot, out nint function)
    {
        function = 0;

        return property is not 0
            && _memory.TryRead(property, out nint vtable) && InMainModule(vtable)
            && _memory.TryRead(vtable + slot, out function) && InMainModule(function);
    }

    private bool ZeroFill(nint address, int length)
    {
        return length <= 0 || _memory.WriteProtected(address, new byte[length]);
    }

    private static unsafe uint InvokePropertyHash(nint function, nint self, nint value)
    {
        return ((delegate* unmanaged<nint, nint, uint>)function)(self, value);
    }

    private static unsafe bool InvokePropertyIdentical(nint function, nint self, nint a, nint b, uint portFlags)
    {
        return ((delegate* unmanaged<nint, nint, nint, uint, byte>)function)(self, a, b, portFlags) is not 0;
    }

    private static unsafe void InvokePropertyUnary(nint function, nint self, nint dest)
    {
        ((delegate* unmanaged<nint, nint, void>)function)(self, dest);
    }

    private static unsafe void InvokePropertyCopy(nint function, nint self, nint dest, nint src)
    {
        ((delegate* unmanaged<nint, nint, nint, void>)function)(self, dest, src);
    }

    private bool TryResolveMalloc(int slot, out nint allocator, out nint function)
    {
        allocator = 0;
        function = 0;

        return _gmalloc is not 0
            && _memory.TryRead(_gmalloc, out allocator) && allocator is not 0
            && _memory.TryRead(allocator, out nint vtable) && InMainModule(vtable)
            && _memory.TryRead(vtable + slot, out function) && InMainModule(function);
    }

    internal nint Malloc(nuint size, uint alignment)
    {
        GameThread.EnsureCurrent("Allocating game memory");

        return TryResolveMalloc(MallocSlot, out var allocator, out var malloc) ? InvokeMalloc(malloc, allocator, size, alignment) : 0;
    }

    internal void Free(nint pointer)
    {
        GameThread.EnsureCurrent("Freeing game memory");

        if (pointer is not 0 && TryResolveMalloc(FreeSlot, out var allocator, out var free))
            InvokeFree(free, allocator, pointer);
    }

    internal bool WriteBytes(nint address, ReadOnlySpan<byte> data)
    {
        GameThread.EnsureCurrent("Writing to game memory");

        return _memory.WriteProtected(address, data);
    }

    internal bool ReadBytes(nint address, Span<byte> destination)
    {
        return _memory.TryRead(address, destination);
    }

    internal nint AllocString(string value, out int chars)
    {
        chars = 0;

        if (value.Length > MaxStringChars)
            return 0;

        chars = value.Length + 1;
        var buffer = Malloc((nuint)chars * sizeof(char), 0);

        if (buffer is 0)
            return 0;

        var wide = new byte[chars * sizeof(char)];
        Encoding.Unicode.GetBytes(value, 0, value.Length, wide, 0);

        if (WriteBytes(buffer, wide))
            return buffer;

        Free(buffer);
        return 0;
    }

    internal bool WriteFString(nint fstringAddress, string value)
    {
        var buffer = AllocString(value, out var chars);

        if (buffer is 0)
            return false;

        var oldData = _memory.TryRead(fstringAddress, out nint data) && data is not 0 && _memory.IsReadable(data, sizeof(char)) ? data : 0;

        if (!_memory.WriteProtected(fstringAddress, buffer)
            || !_memory.WriteProtected(fstringAddress + nint.Size, chars)
            || !_memory.WriteProtected(fstringAddress + nint.Size + sizeof(int), chars))
        {
            Free(buffer);
            return false;
        }

        if (oldData is not 0)
            Free(oldData);

        return true;
    }

    internal byte[] StringValueBytes(string value)
    {
        var fstring = new byte[nint.Size + 2 * sizeof(int)];
        var buffer = AllocString(value, out var chars);

        if (buffer is 0)
            return fstring;

        BitConverter.GetBytes(buffer).CopyTo(fstring, 0);
        BitConverter.GetBytes(chars).CopyTo(fstring, nint.Size);
        BitConverter.GetBytes(chars).CopyTo(fstring, nint.Size + sizeof(int));

        return fstring;
    }

    internal void ReleaseStringValue(byte[] fstring)
    {
        if (fstring.Length >= nint.Size && (nint)BitConverter.ToInt64(fstring, 0) is var buffer && buffer is not 0)
            Free(buffer);
    }

    private bool BuildStringArgument(nint slot, byte[] utf16)
    {
        var fstring = StringValueBytes(Encoding.Unicode.GetString(utf16));

        if (WriteBytes(slot, fstring))
            return true;

        ReleaseStringValue(fstring);

        return false;
    }

    internal bool WriteName(nint slotAddress, string name)
    {
        return _names.TryIntern(name, out var fname) && _memory.WriteProtected(slotAddress, fname);
    }

    private static unsafe nint InvokeMalloc(nint function, nint self, nuint size, uint alignment)
    {
        return ((delegate* unmanaged<nint, nuint, uint, nint>)function)(self, size, alignment);
    }

    private static unsafe void InvokeFree(nint function, nint self, nint pointer)
    {
        ((delegate* unmanaged<nint, nint, void>)function)(self, pointer);
    }

    internal nint EnumOf(nint property)
    {
        return Pointer(property, _propertyBaseSize + nint.Size);
    }

    internal UEnum? WrapEnum(nint address)
    {
        return address is 0 ? null : new UEnum(address, this);
    }

    internal nint StructOf(nint property)
    {
        return Pointer(property, _propertyBaseSize);
    }

    internal nint ByteEnumOf(nint property)
    {
        return Pointer(property, _propertyBaseSize);
    }

    internal UScriptStruct? WrapScriptStruct(nint address)
    {
        return address is 0 ? null : new UScriptStruct(address, this);
    }

    internal nint InnerOf(nint property)
    {
        return Pointer(property, _propertyBaseSize);
    }

    internal nint MapKeyOf(nint property)
    {
        return Pointer(property, _propertyBaseSize);
    }

    internal nint MapValueOf(nint property)
    {
        return Pointer(property, _propertyBaseSize + nint.Size);
    }

    internal int MapValueOffsetOf(nint property)
    {
        return _memory.TryRead(property + _propertyBaseSize + 2 * nint.Size, out int value) ? value : 0;
    }

    internal int MapPairStrideOf(nint property)
    {
        return _memory.TryRead(property + _propertyBaseSize + 2 * nint.Size + 0x14, out int value) ? value : 0;
    }

    internal nint SetElementOf(nint property)
    {
        return Pointer(property, _propertyBaseSize);
    }

    internal int SetStrideOf(nint property)
    {
        return _memory.TryRead(property + _propertyBaseSize + nint.Size + 0x10, out int value) ? value : 0;
    }

    internal ContainerLayout MapLayoutOf(nint property)
    {
        var layout = property + _propertyBaseSize + 2 * nint.Size;

        return new ContainerLayout
        {
            KeyProperty = MapKeyOf(property),
            ValueProperty = MapValueOf(property),
            ValueOffset = ReadInt(layout + 0x00),
            HashNextIdOffset = ReadInt(layout + 0x04),
            HashIndexOffset = ReadInt(layout + 0x08),
            SlotStride = ReadInt(layout + 0x14),
            IsMap = true
        };
    }

    internal ContainerLayout SetLayoutOf(nint property)
    {
        var layout = property + _propertyBaseSize + nint.Size;

        return new ContainerLayout
        {
            KeyProperty = SetElementOf(property),
            HashNextIdOffset = ReadInt(layout + 0x00),
            HashIndexOffset = ReadInt(layout + 0x04),
            SlotStride = ReadInt(layout + 0x10),
            IsMap = false
        };
    }

    internal bool TryReadHashTable(nint set, out nint buckets, out int hashSize)
    {
        buckets = 0;

        if (!_memory.TryRead(set + SetHashSizeOffset, out hashSize) || hashSize <= 0 || hashSize > MaxSaneCount)
            return false;

        var heap = _memory.TryRead(set + SetHashHeapOffset, out nint pointer) ? pointer : 0;
        buckets = heap is not 0 ? heap : set + SetHashInlineOffset;

        return true;
    }

    internal int FindInContainer(nint set, in ContainerLayout layout, nint keyPtr)
    {
        if (!layout.IsValid || !PropertyHash(layout.KeyProperty, keyPtr, out var hash) || !TryReadHashTable(set, out var buckets, out var hashSize))
            return InvalidId;

        if (!_memory.TryRead(set + SparseDataOffset, out nint data) || data is 0)
            return InvalidId;

        var bucket = (int)(hash & (uint)(hashSize - 1));

        if (!_memory.TryRead(buckets + (nint)bucket * sizeof(int), out int id))
            return InvalidId;

        for (var guard = 0; id is not InvalidId && guard < MaxHashChain; guard++)
        {
            var element = data + (nint)id * layout.SlotStride;

            if (PropertyIdentical(layout.KeyProperty, element, keyPtr))
                return id;

            if (!_memory.TryRead(element + layout.HashNextIdOffset, out id))
                return InvalidId;
        }

        return InvalidId;
    }

    private int ReadInt(nint address)
    {
        return _memory.TryRead(address, out int value) ? value : 0;
    }

    internal bool ContainerAdd(nint set, in ContainerLayout layout, nint keyPtr, nint valuePtr)
    {
        if (!layout.IsValid)
            return false;

        var existing = FindInContainer(set, layout, keyPtr);

        if (existing is not InvalidId)
        {
            if (!layout.IsMap || !_memory.TryRead(set + SparseDataOffset, out nint present) || present is 0)
                return !layout.IsMap;

            var slot = present + (nint)existing * layout.SlotStride + layout.ValueOffset;

            return PropertyDestroy(layout.ValueProperty, slot) && PropertyInitialize(layout.ValueProperty, slot) && PropertyCopySingle(layout.ValueProperty, slot, valuePtr);
        }

        var index = SparseAddUninitialized(set, layout.SlotStride);

        if (index is InvalidId || !_memory.TryRead(set + SparseDataOffset, out nint data) || data is 0)
            return false;

        var element = data + (nint)index * layout.SlotStride;

        if (!PropertyInitialize(layout.KeyProperty, element) || !PropertyCopySingle(layout.KeyProperty, element, keyPtr))
            return false;

        if (layout.IsMap && (!PropertyInitialize(layout.ValueProperty, element + layout.ValueOffset) || !PropertyCopySingle(layout.ValueProperty, element + layout.ValueOffset, valuePtr)))
            return false;

        var hashSize = ReadInt(set + SetHashSizeOffset);

        if (hashSize <= 0 || hashSize < NumberOfHashBuckets(SparseCount(set)))
        {
            Rehash(set, layout);
            return true;
        }

        return PropertyHash(layout.KeyProperty, element, out var hash) && HashLink(set, layout, index, hash, hashSize);
    }

    internal bool ContainerRemove(nint set, in ContainerLayout layout, nint keyPtr)
    {
        if (!layout.IsValid)
            return false;

        var index = FindInContainer(set, layout, keyPtr);

        if (index is InvalidId || !HashUnlink(set, layout, index) || !_memory.TryRead(set + SparseDataOffset, out nint data) || data is 0)
            return false;

        var element = data + (nint)index * layout.SlotStride;

        PropertyDestroy(layout.KeyProperty, element);

        if (layout.IsMap)
            PropertyDestroy(layout.ValueProperty, element + layout.ValueOffset);

        SparseRemoveAt(set, layout.SlotStride, index);

        return true;
    }

    internal bool ContainerAddBytes(nint set, in ContainerLayout layout, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        var scratch = Malloc((nuint)layout.SlotStride, 0);

        if (scratch is 0)
            return false;

        try
        {
            if (!WriteBytes(scratch, key) || (layout.IsMap && !WriteBytes(scratch + layout.ValueOffset, value)))
                return false;

            return ContainerAdd(set, layout, scratch, layout.IsMap ? scratch + layout.ValueOffset : 0);
        }
        finally
        {
            Free(scratch);
        }
    }

    internal bool ContainerRemoveBytes(nint set, in ContainerLayout layout, ReadOnlySpan<byte> key)
    {
        var scratch = Malloc((nuint)layout.SlotStride, 0);

        if (scratch is 0)
            return false;

        try
        {
            return WriteBytes(scratch, key) && ContainerRemove(set, layout, scratch);
        }
        finally
        {
            Free(scratch);
        }
    }

    internal bool ContainerContainsBytes(nint set, in ContainerLayout layout, ReadOnlySpan<byte> key)
    {
        var scratch = Malloc((nuint)layout.SlotStride, 0);

        if (scratch is 0)
            return false;

        try
        {
            return WriteBytes(scratch, key) && FindInContainer(set, layout, scratch) is not InvalidId;
        }
        finally
        {
            Free(scratch);
        }
    }

    private int SparseAddUninitialized(nint sparse, int stride)
    {
        var numFree = ReadInt(sparse + SparseNumFreeOffset);
        int index;

        if (numFree > 0)
        {
            index = ReadInt(sparse + SparseFirstFreeOffset);

            if (index is InvalidId || !_memory.TryRead(sparse + SparseDataOffset, out nint data) || data is 0)
                return InvalidId;

            var nextFree = ReadInt(data + (nint)index * stride + sizeof(int));

            _memory.WriteProtected(sparse + SparseFirstFreeOffset, nextFree);
            _memory.WriteProtected(sparse + SparseNumFreeOffset, numFree - 1);

            if (numFree - 1 > 0 && nextFree is not InvalidId)
                _memory.WriteProtected(data + (nint)nextFree * stride, InvalidId);
        }
        else if (!SparseGrowData(sparse, stride, out index))
        {
            return InvalidId;
        }
        else
        {
            BitAdd(sparse);
        }

        SetAllocBit(sparse, index, true);

        return index;
    }

    private void SparseRemoveAt(nint sparse, int stride, int index)
    {
        if (!_memory.TryRead(sparse + SparseDataOffset, out nint data) || data is 0)
            return;

        var numFree = ReadInt(sparse + SparseNumFreeOffset);
        var link = data + (nint)index * stride;

        _memory.WriteProtected(link, InvalidId);
        _memory.WriteProtected(link + sizeof(int), numFree > 0 ? ReadInt(sparse + SparseFirstFreeOffset) : InvalidId);

        if (numFree > 0)
        {
            var oldFirst = ReadInt(sparse + SparseFirstFreeOffset);

            if (oldFirst is not InvalidId)
                _memory.WriteProtected(data + (nint)oldFirst * stride, index);
        }

        _memory.WriteProtected(sparse + SparseFirstFreeOffset, index);
        _memory.WriteProtected(sparse + SparseNumFreeOffset, numFree + 1);
        SetAllocBit(sparse, index, false);
    }

    private bool SparseGrowData(nint sparse, int stride, out int index)
    {
        index = InvalidId;

        if (!TryReadArrayHeader(sparse + SparseDataOffset, out var data, out var num, out var max))
            return false;

        if (num == max)
        {
            var newMax = GrowArrayMax(num + 1);
            var buffer = Malloc((nuint)newMax * (nuint)stride, 0);

            if (buffer is 0 || (num > 0 && !RelocateBytes(data, buffer, num * stride)) || !WriteArrayHeader(sparse + SparseDataOffset, buffer, num + 1, newMax))
            {
                Free(buffer);
                return false;
            }

            if (data is not 0)
                Free(data);
        }
        else if (!_memory.WriteProtected(sparse + SparseNumOffset, num + 1))
        {
            return false;
        }

        index = num;

        return true;
    }

    private nint BitWords(nint sparse)
    {
        var heap = _memory.TryRead(sparse + SparseBitHeapOffset, out nint pointer) ? pointer : 0;

        return heap is not 0 ? heap : sparse + SparseBitInlineOffset;
    }

    private void SetAllocBit(nint sparse, int index, bool value)
    {
        var address = BitWords(sparse) + (nint)(index >> 5) * sizeof(uint);

        if (!_memory.TryRead(address, out uint word))
            return;

        var mask = 1u << (index & 31);

        _memory.WriteProtected(address, value ? word | mask : word & ~mask);
    }

    private int BitAdd(nint sparse)
    {
        var numBits = ReadInt(sparse + SparseBitNumOffset);

        BitReserve(sparse, numBits + 1);
        _memory.WriteProtected(sparse + SparseBitNumOffset, numBits + 1);
        SetAllocBit(sparse, numBits, false);

        return numBits;
    }

    private void BitReserve(nint sparse, int requiredBits)
    {
        if (requiredBits <= ReadInt(sparse + SparseBitMaxOffset))
            return;

        var oldHeap = _memory.TryRead(sparse + SparseBitHeapOffset, out nint heap) ? heap : 0;

        if (oldHeap is 0 && requiredBits <= SparseInlineWords * 32)
        {
            _memory.WriteProtected(sparse + SparseBitMaxOffset, SparseInlineWords * 32);
            return;
        }

        var oldWords = (ReadInt(sparse + SparseBitMaxOffset) + 31) / 32;
        var newWords = Math.Max((requiredBits + 31) / 32, Math.Max(oldWords * 2, SparseInlineWords));
        var buffer = Malloc((nuint)newWords * sizeof(uint), 0);

        if (buffer is 0 || !WriteBytes(buffer, new byte[newWords * sizeof(uint)]) || (oldWords > 0 && !RelocateBytes(BitWords(sparse), buffer, oldWords * sizeof(uint))))
        {
            Free(buffer);
            return;
        }

        _memory.WriteProtected(sparse + SparseBitHeapOffset, buffer);
        _memory.WriteProtected(sparse + SparseBitMaxOffset, newWords * 32);

        if (oldHeap is not 0)
            Free(oldHeap);
    }

    private nint HashBuckets(nint set)
    {
        var heap = _memory.TryRead(set + SetHashHeapOffset, out nint pointer) ? pointer : 0;

        return heap is not 0 ? heap : set + SetHashInlineOffset;
    }

    private bool HashLink(nint set, in ContainerLayout layout, int index, uint hash, int hashSize)
    {
        if (!_memory.TryRead(set + SparseDataOffset, out nint data) || data is 0)
            return false;

        var buckets = HashBuckets(set);
        var bucket = (nint)(hash & (uint)(hashSize - 1));
        var element = data + (nint)index * layout.SlotStride;

        _memory.WriteProtected(element + layout.HashIndexOffset, (int)bucket);
        _memory.WriteProtected(element + layout.HashNextIdOffset, ReadInt(buckets + bucket * sizeof(int)));
        _memory.WriteProtected(buckets + bucket * sizeof(int), index);

        return true;
    }

    private bool HashUnlink(nint set, in ContainerLayout layout, int index)
    {
        if (!_memory.TryRead(set + SparseDataOffset, out nint data) || data is 0)
            return false;

        var element = data + (nint)index * layout.SlotStride;
        var bucket = ReadInt(element + layout.HashIndexOffset);
        var link = HashBuckets(set) + (nint)bucket * sizeof(int);

        for (var guard = 0; guard < MaxHashChain; guard++)
        {
            var id = ReadInt(link);

            if (id is InvalidId)
                return false;

            if (id == index)
                return _memory.WriteProtected(link, ReadInt(element + layout.HashNextIdOffset));

            link = data + (nint)id * layout.SlotStride + layout.HashNextIdOffset;
        }

        return false;
    }

    private void Rehash(nint set, in ContainerLayout layout)
    {
        HashAllocate(set, NumberOfHashBuckets(SparseCount(set)));

        if (!_memory.TryRead(set + SparseDataOffset, out nint data) || data is 0)
            return;

        var buckets = HashBuckets(set);
        var hashSize = ReadInt(set + SetHashSizeOffset);
        var num = ReadInt(set + SparseNumOffset);

        for (var i = 0; i < num; i++)
        {
            if (!GetAllocBit(set, i))
                continue;

            var element = data + (nint)i * layout.SlotStride;

            if (!PropertyHash(layout.KeyProperty, element, out var hash))
                continue;

            var bucket = (nint)(hash & (uint)(hashSize - 1));

            _memory.WriteProtected(element + layout.HashIndexOffset, (int)bucket);
            _memory.WriteProtected(element + layout.HashNextIdOffset, ReadInt(buckets + bucket * sizeof(int)));
            _memory.WriteProtected(buckets + bucket * sizeof(int), i);
        }
    }

    private void HashAllocate(nint set, int hashSize)
    {
        var oldHeap = _memory.TryRead(set + SetHashHeapOffset, out nint heap) ? heap : 0;
        nint buckets;

        if (hashSize <= 1)
        {
            buckets = set + SetHashInlineOffset;
            _memory.WriteProtected(set + SetHashHeapOffset, (nint)0);
        }
        else
        {
            buckets = Malloc((nuint)hashSize * sizeof(int), 0);

            if (buckets is 0)
                return;

            _memory.WriteProtected(set + SetHashHeapOffset, buckets);
        }

        _memory.WriteProtected(set + SetHashSizeOffset, hashSize);

        var empty = new byte[hashSize * sizeof(int)];
        empty.AsSpan().Fill(0xFF);
        WriteBytes(buckets, empty);

        if (oldHeap is not 0)
            Free(oldHeap);
    }

    private bool GetAllocBit(nint sparse, int index)
    {
        return _memory.TryRead(BitWords(sparse) + (nint)(index >> 5) * sizeof(uint), out uint word) && ((word >> (index & 31)) & 1u) is not 0;
    }

    private static int NumberOfHashBuckets(int count)
    {
        return count >= 4 ? (int)BitOperations.RoundUpToPowerOf2((uint)(count / 2 + 8)) : 1;
    }

    internal int SparseCount(nint sparse)
    {
        if (!_memory.TryRead(sparse + SparseNumOffset, out int num) || num < 0 || num > MaxSaneCount)
            return 0;

        var free = _memory.TryRead(sparse + SparseNumFreeOffset, out int freed) ? freed : 0;

        return num - free;
    }

    internal IEnumerable<nint> SparseElements(nint sparse, int stride)
    {
        if (stride <= 0 || !_memory.TryRead(sparse + SparseDataOffset, out nint data) || data is 0)
            yield break;

        if (!_memory.TryRead(sparse + SparseNumOffset, out int num) || num < 0 || num > MaxSaneCount)
            yield break;

        var heap = _memory.TryRead(sparse + SparseBitHeapOffset, out nint pointer) ? pointer : 0;

        for (var index = 0; index < num; index++)
        {
            var word = index >> 5;

            if (heap is 0 && word >= SparseInlineWords)
                yield break;

            var wordAddress = (heap is not 0 ? heap : sparse + SparseBitInlineOffset) + word * sizeof(uint);

            if (!_memory.TryRead(wordAddress, out uint bits))
                yield break;

            if (((bits >> (index & 31)) & 1u) is not 0)
                yield return data + index * stride;
        }
    }

    internal bool TryReadArray(nint address, out nint data, out int count)
    {
        count = 0;

        if (!_memory.TryRead(address, out data) || data is 0)
            return false;

        if (!_memory.TryRead(address + nint.Size, out count) || count < 0 || count > MaxSaneCount)
        {
            count = 0;
            return false;
        }

        return true;
    }

    internal int ArrayInsert(nint header, nint inner, int stride, int index, ReadOnlySpan<byte> element)
    {
        if (stride <= 0 || element.Length != stride || !TryReadArrayHeader(header, out var data, out var num, out var max) || (uint)index > (uint)num)
            return -1;

        if (num == max)
            return ArrayInsertGrowing(header, inner, stride, index, element, data, num);

        if (index < num && !RelocateBytes(data + (nint)index * stride, data + (nint)(index + 1) * stride, (num - index) * stride))
            return -1;

        if (!PlaceElement(inner, data + (nint)index * stride, element) || !_memory.WriteProtected(header + nint.Size, num + 1))
            return -1;

        return index;
    }

    private bool PlaceElement(nint inner, nint dest, ReadOnlySpan<byte> element)
    {
        return inner is 0 ? WriteBytes(dest, element) : PropertyConstructFrom(inner, dest, element);
    }

    private void DestroyElement(nint inner, nint slot)
    {
        if (inner is not 0)
            PropertyDestroy(inner, slot);
    }

    internal bool ArrayRemoveAt(nint header, nint inner, int stride, int index, int count)
    {
        if (stride <= 0 || count <= 0 || !TryReadArrayHeader(header, out var data, out var num, out _) || index < 0 || index + count > num)
            return false;

        for (var i = 0; i < count; i++)
            DestroyElement(inner, data + (nint)(index + i) * stride);

        var tail = num - (index + count);

        if (tail > 0 && !RelocateBytes(data + (nint)(index + count) * stride, data + (nint)index * stride, tail * stride))
            return false;

        return _memory.WriteProtected(header + nint.Size, num - count);
    }

    internal bool ArrayClear(nint header, nint inner, int stride)
    {
        if (!TryReadArrayHeader(header, out var data, out var num, out _))
            return false;

        for (var i = 0; i < num; i++)
            DestroyElement(inner, data + (nint)i * stride);

        return _memory.WriteProtected(header + nint.Size, 0);
    }

    private int ArrayInsertGrowing(nint header, nint inner, int stride, int index, ReadOnlySpan<byte> element, nint data, int num)
    {
        var newMax = GrowArrayMax(num + 1);
        var buffer = Malloc((nuint)newMax * (nuint)stride, 0);

        if (buffer is 0)
            return -1;

        if ((index > 0 && !RelocateBytes(data, buffer, index * stride))
            || (index < num && !RelocateBytes(data + (nint)index * stride, buffer + (nint)(index + 1) * stride, (num - index) * stride))
            || !PlaceElement(inner, buffer + (nint)index * stride, element)
            || !WriteArrayHeader(header, buffer, num + 1, newMax))
        {
            Free(buffer);
            return -1;
        }

        if (data is not 0)
            Free(data);

        return index;
    }

    private bool TryReadArrayHeader(nint header, out nint data, out int num, out int max)
    {
        num = 0;
        max = 0;

        return _memory.TryRead(header, out data)
            && _memory.TryRead(header + nint.Size, out num)
            && _memory.TryRead(header + nint.Size + sizeof(int), out max)
            && num >= 0 && max >= num && num <= MaxSaneCount;
    }

    private bool WriteArrayHeader(nint header, nint data, int num, int max)
    {
        return _memory.WriteProtected(header, data)
            && _memory.WriteProtected(header + nint.Size, num)
            && _memory.WriteProtected(header + nint.Size + sizeof(int), max);
    }

    private bool RelocateBytes(nint from, nint to, int length)
    {
        if (length <= 0)
            return true;

        var buffer = new byte[length];

        return _memory.TryRead(from, buffer) && _memory.WriteProtected(to, buffer);
    }

    private static int GrowArrayMax(int needed)
    {
        return needed <= 4 ? 4 : needed + needed / 2;
    }

    internal IEnumerable<(string Name, long Value)> EnumEntries(nint enumAddress)
    {
        if (!_memory.TryRead(enumAddress + _enumNames, out nint data) || data is 0)
            yield break;

        if (!_memory.TryRead(enumAddress + _enumNames + nint.Size, out int count) || count <= 0 || count > MaxEnumEntries)
            yield break;

        for (var index = 0; index < count; index++)
        {
            if (!_memory.TryRead(data + index * EnumPairStride, out int nameId) || !_memory.TryRead(data + index * EnumPairStride + sizeof(long), out long value))
                yield break;

            yield return (ResolveName(nameId), value);
        }
    }

    internal IEnumerable<FProperty> PropertiesOf(nint structAddress)
    {
        for (var field = Pointer(structAddress, _childProperties); field is not 0; field = FieldNextOf(field))
            yield return WrapPropertyTyped(field);
    }

    internal IEnumerable<UFunction> FunctionsOf(nint structAddress)
    {
        for (var child = Pointer(structAddress, _children); child is not 0; child = Pointer(child, _ufieldNext))
        {
            if (Wrap(child) is UFunction function)
                yield return function;
        }
    }

    internal bool TryReadValue<T>(nint address, out T value)
        where T : unmanaged
    {
        return _memory.TryRead(address, out value);
    }

    internal bool WriteValue<T>(nint address, in T value)
        where T : unmanaged
    {
        return _memory.WriteProtected(address, value);
    }

    internal T ValueAt<T>(nint address)
        where T : unmanaged
    {
        return TryReadValue(address, out T value) ? value : default;
    }

    internal UObject? ObjectAt(nint address)
    {
        return TryReadValue(address, out nint pointer) && pointer is not 0 ? WrapOrNull(pointer) : null;
    }

    internal string StringAt(nint address)
    {
        return ReadFString(address);
    }

    internal string NameAt(nint address)
    {
        return NameAtSlot(address);
    }

    internal byte[] BytesAt(nint address, int size)
    {
        var bytes = new byte[size];
        ReadBytes(address, bytes);

        return bytes;
    }

    internal byte[] NameBytes(string name)
    {
        return _names.TryIntern(name, out var fname) ? BitConverter.GetBytes(fname) : new byte[sizeof(long)];
    }

    private nint FindMetaClass()
    {
        if (!_names.TryFind("Class", out var classId) || classId is 0)
            return 0;

        foreach (var address in _objects.Addresses())
        {
            if (NameIdOf(address) == classId && ClassPointerOf(address) == address)
                return address;
        }

        return 0;
    }

    private nint FindClassNamed(string name)
    {
        if (_classMeta is 0 || !_names.TryFind(name, out var id) || id is 0)
            return 0;

        foreach (var address in _objects.Addresses())
        {
            if (NameIdOf(address) == id && ClassPointerOf(address) == _classMeta)
                return address;
        }

        return 0;
    }
}