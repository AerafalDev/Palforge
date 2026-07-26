using Microsoft.Extensions.Logging;

namespace Palforge.Unreal.Threading;

internal static class GameThreadGuard
{
    public static void Run(Action callback, ILogger log, string context)
    {
        try
        {
            callback();
        }
        catch (Exception exception)
        {
            log.LogError(exception, "{Context} threw on the game thread and was swallowed to keep the game running", context);
        }
    }
}