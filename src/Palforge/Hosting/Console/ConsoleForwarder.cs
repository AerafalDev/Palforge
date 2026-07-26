using System.Text;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Console;
using Microsoft.Win32.SafeHandles;
using NativeConsole = System.Console;

namespace Palforge.Hosting.Console;

internal static class ConsoleForwarder
{
    private static SafeFileHandle? _nullDevice;
    private static SafeFileHandle? _consoleOutput;

    public static void CreateConsole()
    {
        EnsureConsole();

        _consoleOutput = OpenConsoleOutput();

        EnableVirtualTerminal(_consoleOutput);
        BindManagedOutput(_consoleOutput);
        SuppressServerOutput();

        PInvoke.SetConsoleOutputCP(PInvoke.CP_UTF8);
        PInvoke.SetConsoleTitle("Palforge Dedicated Server");
    }

    private static void EnsureConsole()
    {
        if (!PInvoke.GetConsoleWindow().IsNull)
            return;

        if (!PInvoke.AttachConsole(PInvoke.ATTACH_PARENT_PROCESS))
            PInvoke.AllocConsole();
    }

    private static SafeFileHandle OpenConsoleOutput()
    {
        return PInvoke.CreateFile(
            PInvoke.CONOUT_FILE,
            PInvoke.GENERIC_READ | PInvoke.GENERIC_WRITE,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
            lpSecurityAttributes: null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
            hTemplateFile: null);
    }

    private static void EnableVirtualTerminal(SafeFileHandle output)
    {
        if (PInvoke.GetConsoleMode(output, out var mode))
            PInvoke.SetConsoleMode(output, mode | CONSOLE_MODE.ENABLE_VIRTUAL_TERMINAL_PROCESSING);
    }

    private static void BindManagedOutput(SafeFileHandle output)
    {
        var writer = new StreamWriter(new FileStream(output, FileAccess.Write), new UTF8Encoding(false)) { AutoFlush = true };

        NativeConsole.SetOut(writer);
        NativeConsole.SetError(writer);
    }

    private static void SuppressServerOutput()
    {
        _nullDevice = PInvoke.CreateFile(
            PInvoke.NULL_FILE,
            PInvoke.GENERIC_WRITE,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
            lpSecurityAttributes: null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
            hTemplateFile: null);

        PInvoke.SetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE, _nullDevice);
        PInvoke.SetStdHandle(STD_HANDLE.STD_ERROR_HANDLE, _nullDevice);
    }
}