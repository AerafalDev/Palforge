using System.Text;
using Palforge.Tests.Memory;
using Palforge.Tests.Unreal.Probes;
using Palforge.Unreal.Names;

namespace Palforge.Tests.Unreal.Names;

internal sealed class FakeFNameNatives : IFNameNatives
{
    private readonly FakeMemory _memory;
    private readonly FakeNameTable _names;

    public int ConstructCalls { get; private set; }

    public int ToStringCalls { get; private set; }

    public FakeFNameNatives(FakeMemory memory, FakeNameTable names)
    {
        _memory = memory;
        _names = names;
    }

    public void Construct(nint self, nint wideName, int findType)
    {
        ConstructCalls++;

        var builder = new StringBuilder();

        for (var offset = 0; _memory.TryRead(wideName + offset, out ushort character) && character is not 0; offset += 2)
            builder.Append((char)character);

        var name = builder.ToString();

        var index = _names.TryFind(name, out var existing)
            ? existing
            : findType is (int)EFindName.Add ? _names.Intern(name) : 0;

        _memory.TryWrite(self, index);
        _memory.TryWrite(self + 4, 0);
    }

    public void ToString(nint self, nint outString)
    {
        ToStringCalls++;

        _memory.TryRead(self, out int id);

        var name = _names.TryResolve(id, out var resolved) ? resolved : string.Empty;
        var bytes = Encoding.Unicode.GetBytes(name + '\0');
        var buffer = _memory.Allocate(bytes.Length);

        _memory.TryWrite(buffer, bytes);
        _memory.TryWrite(outString, buffer);
        _memory.TryWrite(outString + 8, name.Length + 1);
        _memory.TryWrite(outString + 12, name.Length + 1);
    }
}