using Microsoft.Extensions.Logging.Abstractions;
using Palforge.Plugins;
using Palforge.Plugins.Watchers;
using Palforge.Unreal;
using Palforge.Unreal.Runtime;

namespace Palforge.Tests.Plugins;

public sealed class PluginScopeTests
{
    [Fact]
    public void RevokeTakesBackEveryRegistrationNewestFirst()
    {
        var order = new List<string>();
        var scope = NewScope();

        scope.Track(new Revocation(() => order.Add("first")));
        scope.Track(new Revocation(() => order.Add("second")));
        scope.Track(new Revocation(() => order.Add("third")));

        scope.Revoke();

        Assert.Equal(["third", "second", "first"], order);
    }

    [Fact]
    public void RevokingTwiceTakesBackEachRegistrationOnce()
    {
        var revocations = 0;
        var scope = NewScope();

        scope.Track(new Revocation(() => revocations++));

        scope.Revoke();
        scope.Revoke();

        Assert.Equal(1, revocations);
    }

    [Fact]
    public void ARegistrationThatThrowsDoesNotStopTheRest()
    {
        var revoked = 0;
        var scope = NewScope();

        scope.Track(new Revocation(() => revoked++));
        scope.Track(new Revocation(() => throw new InvalidOperationException("refuses to go")));
        scope.Track(new Revocation(() => revoked++));

        scope.Revoke();

        Assert.Equal(2, revoked);
    }

    [Fact]
    public void TrackingAfterRevocationDisposesTheRegistrationAndThrows()
    {
        var scope = NewScope();

        scope.Revoke();

        var disposed = false;

        Assert.Throws<ObjectDisposedException>(() => scope.Track(new Revocation(() => disposed = true)));
        Assert.True(disposed);
    }

    private static PluginScope NewScope()
    {
        var runtime = UnrealRuntime.Disabled("not needed for this test");

        var objects = new ObjectWatcher(runtime, NullLogger.Instance);

        return new PluginScope("Test", runtime, new UnrealApi(runtime), objects, new WorldWatcher(runtime, objects, NullLogger.Instance), NullLogger.Instance);
    }

    private sealed class Revocation(Action onDispose) : IDisposable
    {
        public void Dispose()
        {
            onDispose();
        }
    }
}