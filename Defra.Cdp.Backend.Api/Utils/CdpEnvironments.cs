namespace Defra.Cdp.Backend.Api.Utils;

public static class CdpEnvironments
{
    public static readonly string[] Environments =
    [
        "infra-dev",
        "management",
        "dev",
        "test",
        "ext-test",
        "perf-test",
        "prod"
    ];
    
    public static readonly string[] EnvironmentExcludingInfraDev =
    [
        "management",
        "dev",
        "test",
        "ext-test",
        "perf-test",
        "prod"
    ];
}