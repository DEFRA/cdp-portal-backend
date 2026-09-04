using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Tests.Utils;

public class SemVerTest
{
    [Fact]
    public void SemVerAllowsForMajMinPatch()
    {
        Assert.True(SemVer.IsSemVer("1.2.3"));
        Assert.True(SemVer.IsSemVer("0.0.0"));
        Assert.True(SemVer.IsSemVer("1.2.3-rc.1"));
        Assert.True(SemVer.IsSemVer("1.2.3+build.1"));
        Assert.True(SemVer.IsSemVer("1.2.3-rc.1+build.1"));
    }

    [Fact]
    public void SemVerRejectsNonSemverVersions()
    {
        Assert.False(SemVer.IsSemVer("1.2.3.4"));
        Assert.False(SemVer.IsSemVer("1.2"));
        Assert.False(SemVer.IsSemVer("1.2.3_special"));
        Assert.False(SemVer.IsSemVer("test 1.2.3.4"));
        Assert.False(SemVer.IsSemVer("123"));
        Assert.False(SemVer.IsSemVer("1.2.3-"));
        Assert.False(SemVer.IsSemVer("1.2.3+"));
    }

    [Fact]
    public void SemVerAllowsForVPrefix()
    {
        Assert.True(SemVer.IsSemVer("v1.2.3"));
    }

    [Fact]
    public void SemVerAsLong()
    {
        var v = SemVer.SemVerAsLong("11.2.3");
        var pa = v & 0xFFFF;
        var mi = (v >> 16) & 0xFFFF;
        var ma = (v >> 32) & 0xFFFF;
        Assert.Equal(3, pa);
        Assert.Equal(2, mi);
        Assert.Equal(11, ma);
    }

    [Fact]
    public void SemVerAsLongIgnoresV()
    {
        Assert.Equal(SemVer.SemVerAsLong("1.2.3"), SemVer.SemVerAsLong("v1.2.3"));
    }

    [Fact]
    public void SemVerAsLongCanBeCompared()
    {
        Assert.True(SemVer.SemVerAsLong("10.10.0") > SemVer.SemVerAsLong("9.9.0"));
        Assert.Equal(SemVer.SemVerAsLong("10.10.0"), SemVer.SemVerAsLong("10.10.0"));
    }

    [Fact]
    public void SemVerAsLongPacksOnlyTheCoreVersion()
    {
        Assert.Equal(SemVer.SemVerAsLong("1.2.3"), SemVer.SemVerAsLong("1.2.3-rc.1"));
        Assert.Equal(SemVer.SemVerAsLong("1.2.3"), SemVer.SemVerAsLong("1.2.3+build.1"));
        Assert.Equal(SemVer.SemVerAsLong("1.2.3-rc.1"), SemVer.SemVerAsLong("1.2.3-rc.2"));
        Assert.Equal(SemVer.SemVerAsLong("1.2.3+build.1"), SemVer.SemVerAsLong("1.2.3+build.2"));
    }

    [Theory]
    [InlineData("0.0.0", 0)]
    [InlineData("0.0.1", 1)]
    [InlineData("1.0.0", 4294967296)]
    [InlineData("0.1.0", 65536)]
    [InlineData("1.2.3", 4295098371)]
    [InlineData("11.2.3", 47244771331)]
    [InlineData("10.10.0", 42950328320)]
    [InlineData("9.9.0", 38655295488)]
    [InlineData("123.456.789", 528310862613)]
    [InlineData("65535.65535.65535", 281474976710655)]
    [InlineData("2024.1.15", 8693013872655)]
    public void SemVerAsLongMatchesIndependentlyComputedValue(string version, long expected)
    {
        Assert.Equal(expected, SemVer.SemVerAsLong(version));
        Assert.Equal(expected, SemVer.SemVerAsLong("v" + version));
    }
}