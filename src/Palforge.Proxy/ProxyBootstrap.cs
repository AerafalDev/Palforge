namespace Palforge.Proxy;

internal static class ProxyBootstrap
{
    private const byte Idle = 0;
    private const byte Initializing = 1;
    private const byte Ready = 2;

    private static byte s_state;

    public static void EnsureStarted()
    {
        var previous = Interlocked.CompareExchange(ref s_state, Initializing, Idle);

        switch (previous)
        {
            case Ready:
                return;
            case Idle:
            {
                var win64Directory = ProxyPath.GetBinariesWin64Directory();

                try
                {
                    ProxyForwarder.Load();

                    new Thread(() => ProxyHost.Start(win64Directory))
                    {
                        IsBackground = true,
                        Name = "Palforge.ProxyHost"
                    }.Start();
                }
                catch (Exception ex)
                {
                    Environment.FailFast("Failed to start Palforge proxy host", ex);
                }
                finally
                {
                    Volatile.Write(ref s_state, Ready);
                }

                return;
            }
        }

        var spinner = new SpinWait();

        while (Volatile.Read(ref s_state) is not Ready)
            spinner.SpinOnce();
    }
}