using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Palforge.Unreal.Reflection;
using Palforge.Unreal.Runtime;

namespace Palforge.Unreal.Diagnostics;

internal static class UnrealSelfCheck
{
    public static void Run(ILogger log, UnrealRuntime runtime)
    {
        var actor = runtime.Reflection.FindClass("Actor");

        if (actor is null)
        {
            log.LogWarning("Reflection self-check: the 'Actor' class was not found in the object array");
            return;
        }

        var chain = new StringBuilder(actor.Name);

        for (var super = actor.SuperClass; super is not null; super = super.SuperClass)
            chain.Append(" -> ").Append(super.Name);

        log.LogInformation("Reflection ready: {Chain}; default object = {DefaultObject}", chain, actor.ClassDefaultObject?.Name ?? "<none>");

        LogLiveInstance(log, runtime, "PalGameSetting", "PalPlayerCharacter", "PalPlayerController", "PalGameStateInGame", "Actor");
        LogKindExamples(log, runtime, PropertyKind.SoftObject, PropertyKind.WeakObject, PropertyKind.Byte, PropertyKind.Interface, PropertyKind.Delegate, PropertyKind.MulticastInlineDelegate, PropertyKind.LazyObject, PropertyKind.Text, PropertyKind.MulticastSparseDelegate, PropertyKind.FieldPath, PropertyKind.Optional);
        LogWriteRoundTrip(log, runtime);
        LogStringWriteRoundTrip(log, runtime);
        LogNameWriteRoundTrip(log, runtime);
        LogStructWriteRoundTrip(log, runtime);
        LogArrayWriteRoundTrip(log, runtime);
        LogMapKeyHashCheck(log, runtime);
        LogMapFindCheck(log, runtime);
        LogMapWriteRoundTrip(log, runtime);
        LogArrayDeepCopyCheck(log, runtime);
        LogFunctionCall(log, runtime);
        LogTextCall(log, runtime);
        LogArrayOutParameter(log, runtime);
        LogStructOutParameter(log, runtime);
        LogSoftWriteRoundTrip(log, runtime);
        LogWeakWriteRoundTrip(log, runtime);
        LogStructAllocation(log, runtime);
        LogStringElementRoundTrip(log, runtime);
        LogTextWriteRoundTrip(log, runtime);
        LogHookCall(log, runtime);
        LogGameWorld(log, runtime);
        LogSpawnActor(log, runtime);
        LogDelegateBind(log, runtime);
    }

    private static void LogDelegateBind(ILogger log, UnrealRuntime runtime)
    {
        LogDelegateBind(log, runtime, "inline", sparse: false);
        LogDelegateBind(log, runtime, "sparse", sparse: true);
    }

    private static void LogDelegateBind(ILogger log, UnrealRuntime runtime, string kind, bool sparse)
    {
        var context = runtime.Reflection;

        if (context.FindLiveExample((owner, property) => sparse
                ? property is FMulticastSparseDelegateProperty sparseProperty && sparseProperty.IsBoundAt(owner.Address)
                : property is FMulticastInlineDelegateProperty) is not ({ } instance, { } delegateProperty)
            || context.FindClass("KismetMathLibrary")?.ClassDefaultObject is not { } target)
        {
            log.LogInformation("Delegate bind ({Kind}): no live delegate or target found", kind);
            return;
        }

        var view = new UnrealMulticastDelegate(instance, delegateProperty.Offset, delegateProperty.Name, sparse);
        var before = view.ToString();
        var added = view.Add(target, "Add_IntInt");
        var during = view.ToString();
        var found = view.Contains(target, "Add_IntInt");
        var removed = view.Remove(target, "Add_IntInt");
        var after = view.ToString();
        var result = added && found && removed && after == before ? "OK" : "MISMATCH";

        log.LogInformation("Delegate bind ({Kind}): {Class}.{Property} add={Added} found={Found} remove={Removed}; before={Before} during={During} after={After} [{Result}]",
            kind, instance.Class?.Name, delegateProperty.Name, added, found, removed, before, during, after, result);
    }

    private static void LogSpawnActor(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;

        if (context.FindClass("Actor") is { } actorClass && context.SpawnActor(actorClass.Address, 0) is { } spawned)
        {
            var isActor = spawned.IsA(actorClass);
            var hasDestroy = spawned.Class?.FindFunction("K2_DestroyActor") is not null;
            var destroyed = context.DestroyActor(spawned);

            log.LogInformation("Spawn actor: spawned '{Name}' (is-actor {IsActor}, has-destroy {HasDestroy}) then destroyed {Destroyed}", spawned.Name, isActor, hasDestroy, destroyed);
        }
        else
            log.LogInformation("Spawn actor: reflected actor spawn returned null [MISMATCH]");

        if (context.FindClass("DataTable") is { } objectClass && context.GameWorld() is { } outer && context.SpawnObject(objectClass.Address, outer.Address) is { } created)
            log.LogInformation("Spawn object: created '{Name}' (is-a {IsA}) [OK]", created.Name, created.IsA(objectClass));
        else
            log.LogInformation("Spawn object: SpawnObject returned null [MISMATCH]");
    }

    private static void LogGameWorld(ILogger log, UnrealRuntime runtime)
    {
        if (runtime.Reflection.GameWorld() is not { } world)
        {
            log.LogInformation("Game world: no live UWorld found");
            return;
        }

        world.TryGet<nint>("OwningGameInstance", out var gameInstance);
        var persistentLevel = world.Class?.FindProperty("PersistentLevel") is not null && world.TryGet<nint>("PersistentLevel", out var level) && level is not 0;

        log.LogInformation("Game world: found '{World}' (game instance {GameInstance}, persistent level {Level}) [OK]", world.Name, gameInstance is not 0, persistentLevel);
    }

    private static void LogHookCall(ILogger log, UnrealRuntime runtime)
    {
        var klass = runtime.Reflection.FindClass("KismetMathLibrary");
        var target = klass?.ClassDefaultObject;
        var function = klass?.FindFunction("Add_IntInt");

        if (target is null || function is null)
        {
            log.LogInformation("Hook call: KismetMathLibrary.Add_IntInt not found");
            return;
        }

        try
        {
            var prefixFired = 0;
            var seenA = int.MinValue;
            var seenReturn = int.MinValue;

            var handle = runtime.Hooks.Register("KismetMathLibrary", "Add_IntInt",
                prefix: context => { prefixFired++; seenA = context.Get<int>("A"); },
                postfix: context => seenReturn = context.GetReturnValue<int>());

            var result = function.Invoke(target, BitConverter.GetBytes(2), BitConverter.GetBytes(3));
            var sum = result is { Length: >= sizeof(int) } ? BitConverter.ToInt32(result) : int.MinValue;

            var firedWhileHooked = prefixFired;
            handle.Dispose();

            function.Invoke(target, BitConverter.GetBytes(7), BitConverter.GetBytes(7));
            var firedAfterDispose = prefixFired - firedWhileHooked;

            var outcome = firedWhileHooked == 1 && seenA == 2 && seenReturn == 5 && sum == 5 && firedAfterDispose == 0 ? "OK" : "MISMATCH";
            log.LogInformation("Hook call: Add_IntInt prefix fired={Fired} sawA={A} postfix return={Return} sum={Sum} afterDispose={After} [{Result}]", firedWhileHooked, seenA, seenReturn, sum, firedAfterDispose, outcome);
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "Hook call on Add_IntInt threw");
        }
    }

    private static void LogTextWriteRoundTrip(ILogger log, UnrealRuntime runtime)
    {
        var examples = runtime.Reflection.FindLiveExamples(PropertyKind.Text);

        if (examples.Count is 0 || examples[0].Property is not FTextProperty text)
            return;

        var instance = examples[0].Instance;

        try
        {
            var original = text.GetTextAt(instance.Address);

            var wrote = text.SetTextAt(instance.Address, "PalforgeText");
            var readBack = text.GetTextAt(instance.Address);

            var restoredOk = text.SetTextAt(instance.Address, original);
            var restored = text.GetTextAt(instance.Address);

            var result = wrote && readBack == "PalforgeText" && restoredOk && restored == original ? "OK" : "MISMATCH";
            log.LogInformation("FText write round-trip: {Class}.{Property} \"{Original}\" -> \"PalforgeText\" (read \"{ReadBack}\") -> restored \"{Restored}\" [{Result}]", instance.Class?.Name, text.Name, original, readBack, restored, result);
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "FText write round-trip on {Property} threw", text.Name);
        }
    }

    private static void LogTextCall(ILogger log, UnrealRuntime runtime)
    {
        var klass = runtime.Reflection.FindClass("KismetStringLibrary");
        var target = klass?.ClassDefaultObject;
        var function = klass?.FindFunction("Concat_StrStr");

        if (target is null || function is null)
        {
            log.LogInformation("Text call: KismetStringLibrary.Concat_StrStr not found");
            return;
        }

        try
        {
            var returned = function.Invoke(target, Encoding.Unicode.GetBytes("Palforge"), Encoding.Unicode.GetBytes("Text"));
            var text = returned is not null ? Encoding.Unicode.GetString(returned) : "<null>";

            log.LogInformation("Text call: Concat_StrStr(\"Palforge\", \"Text\") = \"{Text}\" [{Result}]", text, text == "PalforgeText" ? "OK" : "MISMATCH");
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "Text call on Concat_StrStr threw");
        }
    }

    private static void LogSoftWriteRoundTrip(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;

        if (context.FindLiveExample(static (_, property) => property.Kind is PropertyKind.SoftObject or PropertyKind.SoftClass) is not ({ } instance, { } soft))
        {
            log.LogInformation("Soft write round-trip: no live soft reference found");
            return;
        }

        try
        {
            var slot = instance.Address + soft.Offset;
            var before = context.SoftObjectPath(slot);

            context.WriteSoftObjectPath(slot, "/Palforge/Probe.Probe");
            var written = context.SoftObjectPath(slot);

            context.WriteSoftObjectPath(slot, before);
            var restored = context.SoftObjectPath(slot);

            var result = written is "/Palforge/Probe.Probe" && restored == before ? "OK" : "MISMATCH";
            log.LogInformation("Soft write round-trip: {Class}.{Property} \"{Before}\" -> \"{Written}\" -> restored \"{Restored}\" [{Result}]",
                instance.Class?.Name, soft.Name, before, written, restored, result);
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "Soft write round-trip threw");
        }
    }

    private static void LogWeakWriteRoundTrip(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;

        if (context.FindLiveExample((owner, property) => property.Kind is PropertyKind.WeakObject
                && context.TryWeakPointer(owner.Address, out _, out var serial) && serial > 0) is not ({ } instance, { } weak))
        {
            log.LogInformation("Weak write round-trip: nothing carrying a serial number was found to reference");
            return;
        }

        try
        {
            var slot = instance.Address + weak.Offset;
            var before = new byte[2 * sizeof(int)];
            context.ReadBytes(slot, before);

            var written = context.WriteWeakObject(slot, instance.Address) ? context.WeakObject(slot)?.Name ?? "<null>" : "<refused>";

            context.WriteWeakObject(slot, 0);
            var cleared = context.WeakObject(slot)?.Name ?? "None";

            context.WriteBytes(slot, before);

            var result = written == instance.Name && cleared is "None" ? "OK" : "MISMATCH";
            log.LogInformation("Weak write round-trip: {Class}.{Property} -> {Written} -> cleared {Cleared} -> restored [{Result}]",
                instance.Class?.Name, weak.Name, written, cleared, result);
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "Weak write round-trip threw");
        }
    }

    private static void LogArrayOutParameter(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;
        var klass = context.FindClass("GameplayStatics");
        var target = klass?.ClassDefaultObject;
        var function = klass?.FindFunction("GetAllActorsOfClass");

        if (target is null || function is null || context.GameWorld() is not { } world || context.FindClass("Actor") is not { } actorClass)
        {
            log.LogInformation("Array out parameter: GameplayStatics.GetAllActorsOfClass or a world was not found");
            return;
        }

        try
        {
            var parameters = function.Parameters.ToList();
            var arguments = parameters.Select(parameter => new byte[parameter.ElementSize]).ToArray();

            BitConverter.GetBytes(world.Address).CopyTo(arguments[0], 0);
            BitConverter.GetBytes(actorClass.Address).CopyTo(arguments[1], 0);

            function.Invoke(target, arguments, out var outputs);

            var captured = outputs.Length > 2 ? outputs[2] : [];
            var count = captured.Length / 8;
            var first = count > 0 && context.WrapOrNull((nint)BitConverter.ToInt64(captured, 0)) is { } actor ? actor.Name : "<none>";

            log.LogInformation("Array out parameter: GetAllActorsOfClass(Actor) returned {Count} actors, first '{First}' [{Result}]",
                count, first, count > 0 ? "OK" : "MISMATCH");
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "Array out parameter check threw");
        }
    }

    private static void LogStructAllocation(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;
        var candidate = context.AllScriptStructs().FirstOrDefault(structType => structType.AllProperties.Any(static property => property is FStrProperty));

        if (candidate is null || candidate.AllProperties.FirstOrDefault(static property => property is FStrProperty) is not FStrProperty member)
        {
            log.LogInformation("Struct allocation: no script struct with a string member was found");
            return;
        }

        try
        {
            var address = context.AllocateStruct(candidate.Address);

            if (address is 0)
            {
                log.LogInformation("Struct allocation: {Struct} ({Size} bytes) could not be allocated [MISMATCH]", candidate.Name, context.PropertiesSizeOf(candidate.Address));
                return;
            }

            var empty = context.ReadFString(address + member.Offset);
            var written = context.WriteFString(address + member.Offset, "PalforgeStruct") ? context.ReadFString(address + member.Offset) : "<write failed>";

            context.DestroyStruct(candidate.Address, address);

            var result = empty.Length is 0 && written is "PalforgeStruct" ? "OK" : "MISMATCH";
            log.LogInformation("Struct allocation: {Struct} ({Size} bytes) .{Member} initialised empty -> \"{Written}\" -> destroyed [{Result}]",
                candidate.Name, context.PropertiesSizeOf(candidate.Address), member.Name, written, result);

            LogStructContainerMutation(log, context);
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "Struct allocation check threw");
        }
    }

    private static void LogStructContainerMutation(ILogger log, UnrealContext context)
    {
        var candidate = context.AllScriptStructs().FirstOrDefault(structType => structType.AllProperties.Any(static property => property is FArrayProperty { Inner: FStrProperty }));

        if (candidate?.AllProperties.FirstOrDefault(static property => property is FArrayProperty { Inner: FStrProperty }) is not FArrayProperty array)
        {
            log.LogInformation("Struct container mutation: no script struct with a string array was found");
            return;
        }

        var address = context.AllocateStruct(candidate.Address);

        if (address is 0)
        {
            log.LogInformation("Struct container mutation: {Struct} could not be allocated [MISMATCH]", candidate.Name);
            return;
        }

        try
        {
            var value = new TypedStructValue(address, context, candidate.Name);
            var view = new UnrealArray<string>(value, array.Offset, array.Inner!.ElementSize, array.Name,
                static (memory, element) => memory.StringAt(element),
                static (memory, element) => memory.StringValueBytes(element),
                static (memory, bytes) => memory.ReleaseStringValue(bytes));

            var index = view.Add("PalforgeInStruct");
            var read = index >= 0 && index < view.Count ? view[index] : "<add failed>";
            var count = view.Count;

            var result = index is 0 && read is "PalforgeInStruct" && count is 1 ? "OK" : "MISMATCH";
            log.LogInformation("Struct container mutation: {Struct}.{Array} empty -> added (read \"{Read}\"), count {Count} [{Result}]",
                candidate.Name, array.Name, read, count, result);
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "Struct container mutation check threw");
        }
        finally
        {
            context.DestroyStruct(candidate.Address, address);
        }
    }

    private sealed class TypedStructValue(nint address, UnrealContext context, string structName) : UStructValue(address, context, structName);

    private static void LogStructOutParameter(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;
        var klass = context.FindClass("KismetMathLibrary");
        var target = klass?.ClassDefaultObject;
        var function = klass?.FindFunction("BreakRotIntoAxes");

        if (target is null || function is null || context.FindFirstOf("PalGameSetting") is not { Class: { } settings } instance)
        {
            log.LogInformation("Struct out parameter: KismetMathLibrary.BreakRotIntoAxes or a live target not found");
            return;
        }

        var parameters = function.Parameters.ToList();

        if (settings.AllProperties.FirstOrDefault(static property => property is FStructProperty { Name: "WorldHUDDisplayOffsetDefault" }) is not FStructProperty destination
            || parameters.Count < 2 || parameters[1].ElementSize != destination.ElementSize)
        {
            log.LogInformation("Struct out parameter: no FVector property matching the out parameter was found");
            return;
        }

        try
        {
            var slot = instance.Address + destination.Offset;
            var before = new byte[destination.ElementSize];
            context.ReadBytes(slot, before);

            var arguments = parameters.Select(parameter => new byte[parameter.ElementSize]).ToArray();
            var destinations = new nint[parameters.Count];
            destinations[1] = slot;

            function.Invoke(target, arguments, destinations, out _);

            var filled = destination.FormatValue(instance.Address);

            context.WriteBytes(slot, before);

            var result = filled is "{X=1, Y=0, Z=0}" ? "OK" : "MISMATCH";
            log.LogInformation("Struct out parameter: BreakRotIntoAxes(identity) filled {Class}.{Property} = {Filled} -> restored {Restored} [{Result}]",
                settings.Name, destination.Name, filled, destination.FormatValue(instance.Address), result);
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "Struct out parameter check threw");
        }
    }

    private static void LogStringElementRoundTrip(ILogger log, UnrealRuntime runtime)
    {
        if (runtime.Reflection.FindLiveExample(static (_, property) => property is FArrayProperty { Inner: FStrProperty }) is not ({ } instance, FArrayProperty { Inner: { } inner } array))
        {
            log.LogInformation("String element round-trip: no live string array found");
            return;
        }

        try
        {
            var view = new UnrealArray<string>(instance, array.Offset, inner.ElementSize, array.Name,
                static (context, element) => context.StringAt(element),
                static (context, value) => context.StringValueBytes(value),
                static (context, bytes) => context.ReleaseStringValue(bytes));

            var before = view.Count;
            var index = view.Add("PalforgeElement");
            var read = index >= 0 && index < view.Count ? view[index] : "<add failed>";
            var removed = index >= 0 && view.RemoveAt(index);
            var after = view.Count;
            var result = index == before && read == "PalforgeElement" && removed && after == before ? "OK" : "MISMATCH";

            log.LogInformation("String element round-trip: {Class}.{Property} len {Before} -> +1 (read \"{Read}\") -> restored {After} [{Result}]",
                instance.Class?.Name, array.Name, before, read, after, result);
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "String element round-trip threw");
        }
    }

    private static void LogFunctionCall(ILogger log, UnrealRuntime runtime)
    {
        var klass = runtime.Reflection.FindClass("KismetMathLibrary");
        var target = klass?.ClassDefaultObject;
        var function = klass?.FindFunction("Add_IntInt");

        if (target is null || function is null)
        {
            log.LogInformation("ProcessEvent call: KismetMathLibrary.Add_IntInt not found");
            return;
        }

        try
        {
            var result = function.Invoke(target, BitConverter.GetBytes(2), BitConverter.GetBytes(3));
            var sum = result is { Length: >= sizeof(int) } ? BitConverter.ToInt32(result) : int.MinValue;
            var outcome = sum == 5 ? "OK" : "MISMATCH";
            log.LogInformation("ProcessEvent call: KismetMathLibrary.Add_IntInt(2, 3) = {Sum} [{Result}]", sum, outcome);
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "ProcessEvent call on Add_IntInt threw");
        }
    }

    private static void LogArrayDeepCopyCheck(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;
        var found = context.FindLiveExample((instance, property) => property is FArrayProperty { Inner.Kind: PropertyKind.Str } array && array.CountAt(instance.Address) > 0);

        if (found is not { Property: FArrayProperty { Inner: { } inner } strings } example)
        {
            log.LogInformation("Array deep-copy check: no live TArray<FString> found");
            return;
        }

        var host = example.Instance;

        try
        {
            var before = strings.CountAt(host.Address);
            var source = new byte[inner.ElementSize];
            context.ReadBytes(strings.ElementAt(host.Address, 0), source);
            var originalPointer = BitConverter.ToInt64(source, 0);
            var originalText = inner.FormatValue(strings.ElementAt(host.Address, 0));

            var index = strings.AddAt(host.Address, source);
            var appended = strings.ElementAt(host.Address, index);
            context.TryReadValue(appended, out nint copyPointer);
            var copyText = inner.FormatValue(appended);

            var removed = strings.RemoveAt(host.Address, before);
            var after = strings.CountAt(host.Address);

            var distinct = copyPointer is not 0 && copyPointer != (nint)originalPointer;
            var result = index == before && copyText == originalText && distinct && removed && after == before ? "OK" : "MISMATCH";
            log.LogInformation("Array deep-copy check: {Class}.{Property} {Text} dup ptr 0x{Original:X}->0x{Copy:X} (distinct {Distinct}) restored {After} [{Result}]", host.Class?.Name, strings.Name, originalText, originalPointer, (long)copyPointer, distinct, after, result);
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "Array deep-copy check on {Property} threw", strings.Name);
        }
    }

    private static void LogMapWriteRoundTrip(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;

        if (context.FindFirstOf("PalGameSetting") is not { Class: { } settings } instance)
            return;

        foreach (var property in settings.AllProperties)
        {
            if (property is not FMapProperty map || map.Key is not { Kind: PropertyKind.Int32 } || map.Value is not { Kind: PropertyKind.Int32 })
                continue;

            try
            {
                var before = map.CountAt(instance.Address);
                var rendered = map.FormatValue(instance.Address);
                var key = BitConverter.GetBytes(987654);
                var value = BitConverter.GetBytes(-987654);

                var added = map.AddAt(instance.Address, key, value);
                var afterAdd = map.CountAt(instance.Address);
                var contains = map.ContainsKeyAt(instance.Address, key);

                var removed = map.RemoveAt(instance.Address, key);
                var afterRemove = map.CountAt(instance.Address);
                var restored = map.FormatValue(instance.Address);

                var result = added && afterAdd == before + 1 && contains && removed && afterRemove == before && restored == rendered ? "OK" : "MISMATCH";
                log.LogInformation("Map write round-trip: {Class}.{Property} count {Before} -> {AfterAdd} (contains {Contains}) -> restored {AfterRemove}, render-match {Match} [{Result}]", settings.Name, map.Name, before, afterAdd, contains, afterRemove, restored == rendered, result);
            }
            catch (Exception exception)
            {
                log.LogWarning(exception, "Map write round-trip on {Property} threw", map.Name);
            }

            return;
        }
    }

    private static void LogMapFindCheck(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;

        if (context.FindFirstOf("PalGameSetting") is not { Class: { } settings } instance)
            return;

        foreach (var property in settings.AllProperties)
        {
            if (property is not FMapProperty map || map.Key is null)
                continue;

            try
            {
                var set = instance.Address + map.Offset;
                var layout = context.MapLayoutOf(map.Address);

                if (!context.TryReadHashTable(set, out _, out var hashSize) || !context.TryReadValue(set, out nint data) || data is 0)
                    return;

                var elements = context.SparseElements(set, layout.SlotStride).ToList();
                var bucketOk = 0;
                var findOk = 0;

                foreach (var element in elements)
                {
                    var index = (int)((element - data) / layout.SlotStride);
                    context.TryReadValue(element + layout.HashIndexOffset, out int storedBucket);

                    if (context.PropertyHash(layout.KeyProperty, element, out var hash) && storedBucket == (int)(hash & (uint)(hashSize - 1)))
                        bucketOk++;

                    if (context.FindInContainer(set, layout, element) == index)
                        findOk++;
                }

                var result = elements.Count > 0 && bucketOk == elements.Count && findOk == elements.Count ? "OK" : "MISMATCH";
                log.LogInformation("Map find check: {Class}.{Property} HashSize={HashSize} entries={Count} bucket-match {BucketOk} find-match {FindOk} [{Result}]", settings.Name, map.Name, hashSize, elements.Count, bucketOk, findOk, result);
            }
            catch (Exception exception)
            {
                log.LogWarning(exception, "Map find check on {Property} threw", map.Name);
            }

            return;
        }
    }

    private static void LogMapKeyHashCheck(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;

        if (context.FindFirstOf("PalGameSetting") is not { Class: { } settings } instance)
            return;

        foreach (var property in settings.AllProperties)
        {
            if (property is not FMapProperty { Key: { Kind: PropertyKind.Int32 } key } map)
                continue;

            try
            {
                var sparse = instance.Address + map.Offset;
                var stride = context.MapPairStrideOf(map.Address);
                var elements = context.SparseElements(sparse, stride).ToList();
                var matched = 0;
                var samples = new StringBuilder();

                foreach (var element in elements)
                {
                    context.TryReadValue(element, out int keyValue);
                    var hashed = context.PropertyHash(key.Address, element, out var hash);

                    if (hashed && hash == unchecked((uint)keyValue))
                        matched++;

                    if (samples.Length < 60)
                        samples.Append(samples.Length > 0 ? ", " : "").Append(keyValue).Append("->").Append(hashed ? hash.ToString(CultureInfo.InvariantCulture) : "?");
                }

                var selfEqual = elements.Count > 0 && context.PropertyIdentical(key.Address, elements[0], elements[0]);
                var crossEqual = elements.Count > 1 && context.PropertyIdentical(key.Address, elements[0], elements[1]);
                var result = matched == elements.Count && elements.Count > 0 && selfEqual && !crossEqual ? "OK" : "MISMATCH";

                log.LogInformation("Property virtual check: {Class}.{Property} keys [{Samples}] hash-match {Matched}/{Total}, Identical self={Self} cross={Cross} [{Result}]", settings.Name, map.Name, samples, matched, elements.Count, selfEqual, crossEqual, result);
            }
            catch (Exception exception)
            {
                log.LogWarning(exception, "Property virtual check on {Property} threw", map.Name);
            }

            return;
        }
    }

    private static void LogArrayWriteRoundTrip(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;

        if (context.FindFirstOf("PalGameSetting") is not { Class: { } settings } instance)
            return;

        foreach (var property in settings.AllProperties)
        {
            if (property is not FArrayProperty array || array.Inner is not { } inner || array.CountAt(instance.Address) is 0)
                continue;

            try
            {
                var before = array.CountAt(instance.Address);
                var first = new byte[inner.ElementSize];
                context.ReadBytes(array.ElementAt(instance.Address, 0), first);

                var index = array.AddAt(instance.Address, first);
                var grown = array.CountAt(instance.Address);
                var appended = index >= 0 ? inner.FormatValue(array.ElementAt(instance.Address, index)) : "<add failed>";

                var removed = array.RemoveAt(instance.Address, before);
                var restored = array.CountAt(instance.Address);

                var result = index == before && grown == before + 1 && removed && restored == before ? "OK" : "MISMATCH";
                log.LogInformation("Array write round-trip: {Class}.{Property} len {Before} -> {Grown} (appended {Appended}) -> restored {Restored} [{Result}]", settings.Name, array.Name, before, grown, appended, restored, result);
            }
            catch (Exception exception)
            {
                log.LogWarning(exception, "Array write round-trip on {Property} threw", array.Name);
            }

            return;
        }
    }

    private static void LogStructWriteRoundTrip(ILogger log, UnrealRuntime runtime)
    {
        if (runtime.Reflection.FindFirstOf("PalGameSetting") is not { Class: { } settings } instance)
            return;

        foreach (var property in settings.AllProperties)
        {
            if (property is not FStructProperty structure || structure.Struct?.Name is not "Vector")
                continue;

            try
            {
                var original = structure.GetValueAt(instance.Address);
                var before = structure.FormatValue(instance.Address);

                var modified = (byte[])original.Clone();
                BitConverter.GetBytes(BitConverter.ToDouble(original, 0) + 1).CopyTo(modified, 0);

                var wrote = structure.SetValueAt(instance.Address, modified);
                var readBack = structure.FormatValue(instance.Address);

                structure.SetValueAt(instance.Address, original);
                var restored = structure.FormatValue(instance.Address);

                var result = wrote && readBack != before && restored == before ? "OK" : "MISMATCH";
                log.LogInformation("Struct write round-trip: {Class}.{Property} {Before} -> {ReadBack} -> restored {Restored} [{Result}]", settings.Name, structure.Name, before, readBack, restored, result);
            }
            catch (Exception exception)
            {
                log.LogWarning(exception, "Struct write round-trip on {Property} threw", structure.Name);
            }

            return;
        }
    }

    private static void LogNameWriteRoundTrip(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;
        var examples = context.FindLiveExamples(PropertyKind.Name);

        if (examples.Count is 0 || examples[0].Property is not FNameProperty name)
            return;

        var instance = examples[0].Instance;
        var slot = instance.Address + name.Offset;

        try
        {
            var original = name.GetNameAt(instance.Address);
            context.TryReadValue(slot, out long originalRaw);

            var written = name.SetNameAt(instance.Address, "PalforgeName");
            var readBack = name.GetNameAt(instance.Address);

            context.WriteValue(slot, originalRaw);
            var restored = name.GetNameAt(instance.Address);

            var result = written && readBack == "PalforgeName" && restored == original ? "OK" : "MISMATCH";
            log.LogInformation("FName write round-trip: {Class}.{Property} \"{Original}\" -> \"PalforgeName\" (read \"{ReadBack}\") -> restored \"{Restored}\" [{Result}]", instance.Class?.Name, name.Name, original, readBack, restored, result);
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "FName write round-trip on {Property} threw", name.Name);
        }
    }

    private static void LogStringWriteRoundTrip(ILogger log, UnrealRuntime runtime)
    {
        var context = runtime.Reflection;
        var examples = context.FindLiveExamples(PropertyKind.Str);

        if (examples.Count is 0 || examples[0].Property is not FStrProperty text)
            return;

        var instance = examples[0].Instance;
        var slot = instance.Address + text.Offset;

        try
        {
            var original = text.GetStringAt(instance.Address);

            context.TryReadValue(slot, out nint originalData);
            context.TryReadValue(slot + nint.Size, out int originalNum);
            context.TryReadValue(slot + nint.Size + sizeof(int), out int originalMax);

            var buffer = context.AllocString("PalforgeWrite", out var chars);

            if (buffer is 0)
            {
                log.LogWarning("FString write round-trip: allocation failed");
                return;
            }

            context.WriteValue(slot, buffer);
            context.WriteValue(slot + nint.Size, chars);
            context.WriteValue(slot + nint.Size + sizeof(int), chars);

            var readBack = text.GetStringAt(instance.Address);

            context.WriteValue(slot, originalData);
            context.WriteValue(slot + nint.Size, originalNum);
            context.WriteValue(slot + nint.Size + sizeof(int), originalMax);
            context.Free(buffer);

            var restored = text.GetStringAt(instance.Address);
            var result = readBack == "PalforgeWrite" && restored == original ? "OK" : "MISMATCH";
            log.LogInformation("FString write round-trip: {Class}.{Property} \"{Original}\" -> \"PalforgeWrite\" (read \"{ReadBack}\") -> restored \"{Restored}\" [{Result}]", instance.Class?.Name, text.Name, original, readBack, restored, result);
        }
        catch (Exception exception)
        {
            log.LogWarning(exception, "FString write round-trip on {Property} threw", text.Name);
        }
    }

    private static void LogWriteRoundTrip(ILogger log, UnrealRuntime runtime)
    {
        if (runtime.Reflection.FindFirstOf("PalGameSetting") is not { Class: { } settings } instance)
            return;

        foreach (var property in settings.AllProperties)
        {
            if (property.Kind is not PropertyKind.Int32)
                continue;

            try
            {
                var original = property.GetAt<int>(instance.Address);
                var probe = original + 1;

                property.SetAt(instance.Address, probe);
                var readBack = property.GetAt<int>(instance.Address);

                property.SetAt(instance.Address, original);
                var restored = property.GetAt<int>(instance.Address);

                var result = readBack == probe && restored == original ? "OK" : "MISMATCH";
                log.LogInformation("Write round-trip: {Class}.{Property} {Original} -> {Probe} (read {ReadBack}) -> restored {Restored} [{Result}]", settings.Name, property.Name, original, probe, readBack, restored, result);
            }
            catch (Exception exception)
            {
                log.LogWarning(exception, "Write round-trip on {Property} threw", property.Name);
            }

            return;
        }
    }

    private static void LogKindExamples(ILogger log, UnrealRuntime runtime, params PropertyKind[] kinds)
    {
        foreach (var (instance, property) in runtime.Reflection.FindLiveExamples(kinds))
            log.LogInformation("Reflection: {Class}.{Property} : {Kind} = {Value}", instance.Class?.Name, property.Name, property.Kind, Describe(property, instance.Address));
    }

    private static void LogLiveInstance(ILogger log, UnrealRuntime runtime, params string[] classNames)
    {
        foreach (var className in classNames)
        {
            if (runtime.Reflection.FindFirstOf(className) is not { Class: { } klass } instance)
                continue;

            var dump = new StringBuilder();
            var count = 0;

            foreach (var property in klass.AllProperties)
            {
                if (count++ >= 24)
                    break;

                dump.Append(CultureInfo.InvariantCulture, $"\n    {property.Name} : {property.Kind} = {Describe(property, instance.Address)}");
            }

            log.LogInformation("Reflection: LIVE {Class} '{Name}' ({Count} properties):{Dump}", className, instance.Name, count, dump);

            var shownKinds = new HashSet<PropertyKind>();

            foreach (var property in klass.AllProperties)
            {
                if (property.Kind is (PropertyKind.Array or PropertyKind.Map or PropertyKind.Set or PropertyKind.Struct) && shownKinds.Add(property.Kind))
                {
                    log.LogInformation("Reflection: {Class}.{Property} : {Kind} = {Value}", className, property.Name, property.Kind, Describe(property, instance.Address));

                    if (shownKinds.Count >= 4)
                        break;
                }
            }

            return;
        }

        log.LogInformation("Reflection: no live instance found for {Classes}", string.Join("/", classNames));
    }

    private static string Describe(FProperty property, nint container)
    {
        try
        {
            return property.FormatValue(container);
        }
        catch (InvalidOperationException)
        {
            return "<unreadable>";
        }
    }
}