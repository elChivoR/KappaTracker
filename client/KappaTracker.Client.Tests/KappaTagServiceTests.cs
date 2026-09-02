using System;
using KappaTracker.Client;
using Xunit;

public class KappaTagServiceTests : IDisposable
{
    public KappaTagServiceTests() => KappaTagService.Reset();
    public void Dispose() => KappaTagService.Reset();

    [Fact]
    public void Decorate_prefixes_a_kappa_id()
    {
        KappaTagService.SetIds(new[] { "abc" });
        Assert.Equal("KAPPA · The Collector", KappaTagService.Decorate("abc", "The Collector"));
    }

    [Fact]
    public void Decorate_leaves_a_non_kappa_id_untouched()
    {
        KappaTagService.SetIds(new[] { "abc" });
        Assert.Equal("Debut", KappaTagService.Decorate("xyz", "Debut"));
    }

    [Fact]
    public void Decorate_is_idempotent()
    {
        KappaTagService.SetIds(new[] { "abc" });
        var once = KappaTagService.Decorate("abc", "The Collector");
        var twice = KappaTagService.Decorate("abc", once);
        Assert.Equal("KAPPA · The Collector", twice);
    }

    [Fact]
    public void Decorate_returns_input_when_not_ready()
    {
        Assert.False(KappaTagService.Ready);
        Assert.Equal("The Collector", KappaTagService.Decorate("abc", "The Collector"));
    }

    [Fact]
    public void Decorate_handles_null_or_empty_name()
    {
        KappaTagService.SetIds(new[] { "abc" });
        Assert.Null(KappaTagService.Decorate("abc", null!));
        Assert.Equal("", KappaTagService.Decorate("abc", ""));
    }

    [Fact]
    public void SetIds_null_throws()
        => Assert.Throws<ArgumentNullException>(() => KappaTagService.SetIds(null!));

    [Fact]
    public void IsKappa_false_for_null_empty_and_unknown()
    {
        KappaTagService.SetIds(new[] { "abc" });
        Assert.False(KappaTagService.IsKappa(null!));
        Assert.False(KappaTagService.IsKappa(""));
        Assert.False(KappaTagService.IsKappa("nope"));
        Assert.True(KappaTagService.IsKappa("abc"));
    }
}
