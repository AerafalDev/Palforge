using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Palforge.Unreal.Reflection;
using Palforge.Unreal.Runtime;

namespace Palforge.Unreal.Diagnostics;

internal static class UnrealDumper
{
    private const string ActorClass = "Actor";

    public static string? Dump(UnrealRuntime runtime, string outputDirectory, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);
        ArgumentNullException.ThrowIfNull(log);

        var context = runtime.Reflection;
        var objects = new StringBuilder();
        var actors = new StringBuilder();
        var total = 0;
        var actorCount = 0;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        foreach (var instance in context.ObjectsFrom(0))
        {
            total++;

            objects.Append(instance.Class?.Name ?? "?").Append(' ').AppendLine(FullNameOf(instance));

            if (!IsActor(instance) || instance.IsDefaultObject)
                continue;

            actorCount++;

            actors.Append(instance.Class?.Name ?? "?").Append(' ').Append(FullNameOf(instance));

            if (LocationOf(instance) is { } location)
                actors.Append(location);

            actors.AppendLine();
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);

            var objectsPath = Path.Combine(outputDirectory, "Objects.txt");
            var actorsPath = Path.Combine(outputDirectory, "Actors.txt");

            File.WriteAllText(objectsPath, objects.ToString());
            File.WriteAllText(actorsPath, actors.ToString());

            log.LogInformation("Objects: dumped {Total} live objects and {Actors} actors in {Elapsed} ms → {Directory}", total, actorCount, stopwatch.ElapsedMilliseconds, outputDirectory);

            return objectsPath;
        }
        catch (Exception exception)
        {
            log.LogError(exception, "Objects: writing the dump failed");

            return null;
        }
    }

    private static string FullNameOf(UObject instance)
    {
        var names = new List<string>(4) { instance.Name };

        for (var outer = instance.Outer; outer is not null; outer = outer.Outer)
            names.Add(outer.Name);

        names.Reverse();

        return string.Join('.', names);
    }

    private static bool IsActor(UObject instance)
    {
        for (var klass = instance.Class; klass is not null; klass = klass.SuperClass)
        {
            if (string.Equals(klass.Name, ActorClass, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string? LocationOf(UObject actor)
    {
        if (actor.Class?.FindProperty("RootComponent") is not FObjectProperty root
            || root.GetObject(actor) is not { } component
            || component.Class?.FindProperty("RelativeLocation") is not FStructProperty location
            || location.GetValueAt(component.Address) is not { Length: >= 24 } bytes)
            return null;

        var x = BitConverter.ToDouble(bytes, 0);
        var y = BitConverter.ToDouble(bytes, sizeof(double));
        var z = BitConverter.ToDouble(bytes, sizeof(double) * 2);

        return string.Create(CultureInfo.InvariantCulture, $" ({x:F1}, {y:F1}, {z:F1})");
    }
}