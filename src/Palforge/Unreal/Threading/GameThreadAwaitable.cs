namespace Palforge.Unreal.Threading;

public readonly struct GameThreadAwaitable
{
    public GameThreadAwaiter GetAwaiter()
    {
        return new GameThreadAwaiter();
    }
}