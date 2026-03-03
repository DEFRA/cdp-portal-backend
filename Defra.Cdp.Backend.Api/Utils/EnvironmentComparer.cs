namespace Defra.Cdp.Backend.Api.Utils;

public class EnvironmentComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        var xValue = Array.FindIndex(CdpEnvironments.Environments, e => e.Equals(x ?? "", StringComparison.CurrentCultureIgnoreCase));
        var yValue = Array.FindIndex(CdpEnvironments.Environments, e => e.Equals(y ?? "", StringComparison.CurrentCultureIgnoreCase));
        return xValue - yValue;
    }
}