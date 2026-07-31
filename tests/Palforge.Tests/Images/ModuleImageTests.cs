using Palforge.Images;
using Palforge.Memory;
using Palforge.Tests.Memory;

namespace Palforge.Tests.Images;

public sealed class ModuleImageTests
{
    private const uint Code = 0x0000_0020;
    private const uint Execute = 0x2000_0000;
    private const uint Read = 0x4000_0000;
    private const uint Write = 0x8000_0000;

    [Fact]
    public void TheTestHostsOwnImageParses()
    {
        Assert.True(ModuleImage.TryParse(new ProbedMemory(), MainModule.BaseAddress, out var image));

        Assert.Equal(MainModule.BaseAddress, image.BaseAddress);
        Assert.True(image.Size > 0);
        Assert.NotEmpty(image.Sections);
        Assert.Contains(image.Sections, static section => section.Name is ".text");
        Assert.Contains(image.ExecutableSections, static section => section.Name is ".text");
    }

    [Fact]
    public void EverySectionOfARealImageLiesInsideIt()
    {
        Assert.True(ModuleImage.TryParse(new ProbedMemory(), MainModule.BaseAddress, out var image));

        foreach (var section in image.Sections)
        {
            Assert.InRange(section.Address, image.BaseAddress, image.BaseAddress + image.Size);
            Assert.InRange(section.End, image.BaseAddress, image.BaseAddress + image.Size);
        }
    }

    [Fact]
    public void TheTestHostsExceptionDirectoryIsFound()
    {
        var memory = new ProbedMemory();

        Assert.True(ModuleImage.TryParse(memory, MainModule.BaseAddress, out var image));

        Assert.NotEqual(0, image.FunctionTable);
        Assert.True(image.FunctionCount > 0);
    }

    [Fact]
    public void AnAddressInsideAFunctionResolvesToThatFunctionsStart()
    {
        var memory = new ProbedMemory();

        Assert.True(ModuleImage.TryParse(memory, MainModule.BaseAddress, out var image));
        Assert.True(memory.TryRead(image.FunctionTable, out uint begin));
        Assert.True(memory.TryRead(image.FunctionTable + 4, out uint end));

        var start = image.BaseAddress + (nint)begin;
        var inside = image.BaseAddress + (nint)((begin + end) / 2);

        Assert.True(image.TryGetFunctionStart(memory, inside, out var resolved));
        Assert.Equal(start, resolved);
    }

    [Fact]
    public void AnAddressOutsideTheImageHasNoFunction()
    {
        var memory = new ProbedMemory();

        Assert.True(ModuleImage.TryParse(memory, MainModule.BaseAddress, out var image));
        Assert.False(image.TryGetFunctionStart(memory, image.BaseAddress - 0x1000, out _));
    }

    [Fact]
    public void AnImageWithoutAnExceptionDirectoryReportsNoFunctions()
    {
        var memory = new FakeMemory();
        var address = new FakeImageBuilder().WithSection(".text", 0x1000, 0x1000, Code | Execute | Read).Build(memory);

        Assert.True(ModuleImage.TryParse(memory, address, out var image));

        Assert.Equal(0, image.FunctionCount);
        Assert.False(image.TryGetFunctionStart(memory, address + 0x1000, out _));
    }

    [Fact]
    public void SectionsAreClassifiedByTheirCharacteristics()
    {
        var memory = new FakeMemory();

        var address = new FakeImageBuilder()
            .WithSection(".text", 0x1000, 0x2000, Code | Execute | Read)
            .WithSection(".rdata", 0x3000, 0x1000, Read)
            .WithSection(".data", 0x4000, 0x1000, Read | Write)
            .Build(memory);

        Assert.True(ModuleImage.TryParse(memory, address, out var image));

        Assert.Equal(3, image.Sections.Count);
        Assert.Single(image.ExecutableSections);
        Assert.Equal(3, image.ReadableSections.Count());

        Assert.True(image.TryGetSection(".text", out var text));
        Assert.Equal(address + 0x1000, text.Address);
        Assert.Equal(0x2000, text.Size);
        Assert.True(text.IsExecutable);
    }

    [Fact]
    public void AnUnknownSectionIsNotInvented()
    {
        var memory = new FakeMemory();
        var address = new FakeImageBuilder().WithSection(".text", 0x1000, 0x2000, Code | Execute | Read).Build(memory);

        Assert.True(ModuleImage.TryParse(memory, address, out var image));
        Assert.False(image.TryGetSection(".pdata", out _));
    }

    [Fact]
    public void AMissingDosSignatureIsRejected()
    {
        var memory = new FakeMemory();
        var builder = new FakeImageBuilder { DosSignature = 0x1234 };

        Assert.False(ModuleImage.TryParse(memory, builder.WithSection(".text", 0x1000, 0x1000, Execute).Build(memory), out _));
    }

    [Fact]
    public void AMissingPeSignatureIsRejected()
    {
        var memory = new FakeMemory();
        var builder = new FakeImageBuilder { PeSignature = 0x1234 };

        Assert.False(ModuleImage.TryParse(memory, builder.WithSection(".text", 0x1000, 0x1000, Execute).Build(memory), out _));
    }

    [Fact]
    public void A32BitImageIsRejected()
    {
        var memory = new FakeMemory();
        var builder = new FakeImageBuilder { Magic = 0x10B };

        Assert.False(ModuleImage.TryParse(memory, builder.WithSection(".text", 0x1000, 0x1000, Execute).Build(memory), out _));
    }

    [Fact]
    public void AnImpossibleSectionCountIsRejected()
    {
        var memory = new FakeMemory();
        var builder = new FakeImageBuilder { SectionCountOverride = 500 };

        Assert.False(ModuleImage.TryParse(memory, builder.WithSection(".text", 0x1000, 0x1000, Execute).Build(memory), out _));
    }

    [Fact]
    public void UnreadableMemoryIsRejectedRatherThanGuessed()
    {
        Assert.False(ModuleImage.TryParse(new FakeMemory(), 0x1000, out _));
    }
}