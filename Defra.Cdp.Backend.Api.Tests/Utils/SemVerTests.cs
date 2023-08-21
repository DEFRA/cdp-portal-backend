using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Tests.Utils;

public class SemVerTest
{
    [Fact]
    public void SemVerAllowsForMajMinPatch()
    {
        Assert.True(SemVer.IsSemVer("1.2.3"));
        Assert.True(SemVer.IsSemVer("0.0.0"));
    }

    [Fact]
    public void SemVerRejectsNonSemverVersions()
    {
        Assert.False(SemVer.IsSemVer("1.2.3.4"));
        Assert.False(SemVer.IsSemVer("1.2"));
        Assert.False(SemVer.IsSemVer("1.2.3_special"));
        Assert.False(SemVer.IsSemVer("test 1.2.3.4"));
        Assert.False(SemVer.IsSemVer("123"));
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
        Assert.Equal(4295098371, SemVer.SemVerAsLong("v1.2.3"));
    }

    [Fact]
    public void SemVerAsLongCanBeCompared()
    {
        Assert.True(SemVer.SemVerAsLong("10.10.0") > SemVer.SemVerAsLong("9.9.0"));
        Assert.Equal(SemVer.SemVerAsLong("10.10.0"), SemVer.SemVerAsLong("10.10.0"));
    }
}