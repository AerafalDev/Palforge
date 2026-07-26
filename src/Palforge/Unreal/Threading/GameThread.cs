namespace Palforge.Unreal.Threading;

public static class GameThread
{
    private static int s_threadId;
    private static Action<Action>? s_post;
    private static Func<int, Action, IDisposable>? s_schedule;

    public static bool IsCurrent =>
        s_threadId is not 0 && Environment.CurrentManagedThreadId == s_threadId;

    public static bool IsAvailable =>
        s_post is not null;

    public static GameThreadAwaitable SwitchTo()
    {
        return new GameThreadAwaitable();
    }

    public static void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsCurrent)
        {
            action();

            return;
        }

        if (s_post is not { } post)
            throw new InvalidOperationException("The game thread is not available yet — the runtime attaches to the engine's Tick during start-up, and nothing can be scheduled onto it before that.");

        post(action);
    }

    public static Task Delay(int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);

        if (s_schedule is not { } schedule)
            throw new InvalidOperationException("The game thread is not available yet — the runtime attaches to the engine's Tick during start-up, and nothing can be scheduled onto it before that.");

        var completion = new TaskCompletionSource();

        schedule(ticks, completion.SetResult);

        return completion.Task;
    }

    public static void EnsureCurrent(string operation)
    {
        if (IsCurrent || s_threadId is 0)
            return;

        throw new InvalidOperationException($"{operation} must run on the game thread, and this is a background thread. Await GameThread.SwitchTo() first, or use GameThread.Post.");
    }

    internal static void MarkCurrent()
    {
        s_threadId = Environment.CurrentManagedThreadId;
    }

    internal static void UsePost(Action<Action> post)
    {
        s_post = post;
    }

    internal static void UseScheduler(Func<int, Action, IDisposable> schedule)
    {
        s_schedule = schedule;
    }

    internal static void Schedule(Action action)
    {
        s_post?.Invoke(action);
    }
}