using Palforge.Memory;

namespace Palforge.Tests.Memory;

public sealed class DirectMemoryTests : MemoryContractTests
{
    private protected override IMemory CreateMemory()
    {
        return new DirectMemory(RegionMap.FromCurrentProcess());
    }
}