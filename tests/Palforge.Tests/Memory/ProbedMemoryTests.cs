using Palforge.Memory;

namespace Palforge.Tests.Memory;

public sealed class ProbedMemoryTests : MemoryContractTests
{
    private protected override IMemory CreateMemory()
    {
        return new ProbedMemory();
    }
}