using System.Diagnostics.CodeAnalysis;
using Defra.Cdp.Backend.Api.Services.Aws;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Utils;

[ExcludeFromCodeCoverage]
public class MockEnvironmentLookup : IEnvironmentLookup
{
    public string? FindEnv(string account)
    {
        return account switch
        {
            "111111111" => "prod",
            "222222222" => "perf-test",
            "333333333" => "dev",
            "444444444" => "test",
            "666666666" => "management",
            "777777777" => "infra-dev",
            "888888888" => "ext-test",
            _ => ""
        };
    }

    public string? FindAccount(string env)
    {
        return env switch
        {
            "prod" => "111111111",
            "perf-test" => "222222222",
            "dev" => "333333333",
            "test" => "444444444",
            "management" => "666666666",
            "infra-dev" => "777777777",
            "ext-test" => "888888888",
            _ => ""
        };
    }

    public string? FindS3BucketSuffix(string env)
    {
        return env switch
        {
            "prod" => "11111",
            "perf-test" => "22222",
            "dev" => "33333",
            "test" => "44444",
            "management" => "66666",
            "infra-dev" => "77777",
            "ext-test" => "88888",
            _ => ""
        };
    }
}