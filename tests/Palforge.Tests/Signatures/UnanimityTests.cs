using Palforge.Signatures;

namespace Palforge.Tests.Signatures;

public sealed class UnanimityTests
{
    [Fact]
    public void NoCandidateIsNotFound()
    {
        var resolution = Unanimity.EnsureOne<nint>([], 7);

        Assert.Equal(ResolutionStatus.NotFound, resolution.Status);
        Assert.False(resolution.TryGetValue(out _));
    }

    [Fact]
    public void OneDistinctValueResolves()
    {
        var resolution = Unanimity.EnsureOne([new Candidate<nint>(0, 0x1000), new Candidate<nint>(1, 0x1000)], 7);

        Assert.True(resolution.TryGetValue(out var address));
        Assert.Equal(0x1000, address);
        Assert.Equal(7, resolution.Attempted);
    }

    [Fact]
    public void AgreementCountsPatternsNotMatchSites()
    {
        var resolution = Unanimity.EnsureOne([new Candidate<nint>(0, 0x1000), new Candidate<nint>(0, 0x1000), new Candidate<nint>(0, 0x1000)], 7);

        Assert.Equal(1, resolution.Agreeing);
        Assert.Equal(3, resolution.Sites);
    }

    [Fact]
    public void TwoDistinctValuesAreAmbiguousWithNoTieBreak()
    {
        var resolution = Unanimity.EnsureOne([new Candidate<nint>(0, 0x1000), new Candidate<nint>(1, 0x1000), new Candidate<nint>(2, 0x2000)], 7);

        Assert.Equal(ResolutionStatus.Ambiguous, resolution.Status);
        Assert.False(resolution.TryGetValue(out _));
    }

    [Fact]
    public void AFailedResolutionNeverYieldsAPlausibleValue()
    {
        Resolution<nint>[] failures =
        [
            Unanimity.EnsureOne<nint>([], 1),
            Unanimity.EnsureOne([new Candidate<nint>(0, 1), new Candidate<nint>(1, 2)], 2)
        ];

        foreach (var resolution in failures)
        {
            Assert.False(resolution.TryGetValue(out var address));
            Assert.Equal(0, address);
            Assert.Throws<InvalidOperationException>(() => resolution.ValueOrThrow());
        }
    }

    [Fact]
    public void UnanimityWorksOnValuesThatAreNotAddresses()
    {
        var agreed = Unanimity.EnsureOne([new Candidate<EngineVersion>(0, new EngineVersion(5, 5)), new Candidate<EngineVersion>(1, new EngineVersion(5, 5))], 33);
        var split = Unanimity.EnsureOne([new Candidate<EngineVersion>(0, new EngineVersion(5, 5)), new Candidate<EngineVersion>(1, new EngineVersion(4, 27))], 33);

        Assert.True(agreed.TryGetValue(out var version));
        Assert.Equal(new EngineVersion(5, 5), version);
        Assert.Equal(ResolutionStatus.Ambiguous, split.Status);
    }
}