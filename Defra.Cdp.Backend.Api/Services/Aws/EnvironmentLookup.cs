namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface IEnvironmentLookup
{
    string? FindEnv(string account);
}

public class EnvironmentLookup : IEnvironmentLookup
{
    private readonly Dictionary<string, string> _envs = new();

    public EnvironmentLookup(IConfiguration cfg)
    {
        var section = cfg.GetSection("EnvironmentMappings");
        foreach (var env in section.GetChildren())
            _envs.Add(env.Key, env.Value!);
    }

    public string? FindEnv(string account)
    {
        return _envs.TryGetValue(account, out var e) ? e : null;
    }
}