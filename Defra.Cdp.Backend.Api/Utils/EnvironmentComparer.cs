namespace Defra.Cdp.Backend.Api.Utils;

public class EnvironmentComparer : IComparer<string>
{
    private readonly string[] _envs =
    [
        "infra-dev",
        "management",
        "dev",
        "test",
        "ext-test",
        "perf-test",
        "prod"
    ];

    public int Compare(string? x, string? y)
    {
        var xValue = Array.FindIndex(_envs, e => e.Equals(x ?? "", StringComparison.CurrentCultureIgnoreCase));
        var yValue = Array.FindIndex(_envs, e => e.Equals(y ?? "", StringComparison.CurrentCultureIgnoreCase));
        return xValue - yValue;
    }
}