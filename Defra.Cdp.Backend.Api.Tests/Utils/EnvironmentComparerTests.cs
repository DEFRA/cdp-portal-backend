using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Tests.Utils;

public class EnvironmentComparerTests
{
    EnvironmentComparer byEnv = new();
    string[] orderedEnvironments = [
        "infra-dev",
        "management",
        "dev",
        "test",
        "ext-test",
        "perf-test",
        "prod"
    ];
    
    [Fact]
    public void SameValuesReturnZero()
    {
        Assert.Equal(0, byEnv.Compare("dev", "dev"));
        Assert.Equal(0, byEnv.Compare("test", "test"));
        Assert.Equal(0, byEnv.Compare("infra-dev", "infra-dev"));
        Assert.Equal(0, byEnv.Compare("prod", "prod"));
        Assert.Equal(0, byEnv.Compare(null, null));
    }

    [Fact]
    public void Sorts_A_List_Of_Environments()
    {
        var data = new List<string> { "perf-test", "prod", "management", "infra-dev", "test", "ext-test", "dev" };
        data.Sort(byEnv);
        Assert.Equal(data, orderedEnvironments);
    }
}