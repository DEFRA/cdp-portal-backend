namespace Defra.Cdp.Backend.Api.Config;

public class SnowOptions
{
    public const string Prefix = "Snow";

    public Dictionary<string, string> Workflows { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}