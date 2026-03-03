namespace Defra.Cdp.Backend.Api.Utils;

public static class CdpEnvironments
{
    public const string InfraDev = "infra-dev";
    public const string Management = "management";
    public const string Dev = "dev";
    public const string Test = "test";
    public const string ExtTest = "ext-test";
    public const string PerfTest = "perf-test";
    public const string Prod = "prod";


    public static readonly string[] Environments =
    [
        InfraDev,
        Management,
        Dev,
        Test,
        ExtTest,
        PerfTest,
        Prod
    ];

    public static readonly string[] EnvironmentExcludingInfraDev =
    [
        Management,
        Dev,
        Test,
        ExtTest,
        PerfTest,
        Prod
    ];
}