namespace Defra.Cdp.Backend.Api.Services.Aws;

public class EnvironmentLookup
{
    private readonly Dictionary<string, string> _envs = new();

    public EnvironmentLookup(IConfiguration cfg)
    {
        var section = cfg.GetSection("EnvironmentMappings");
        foreach (var env in section.GetChildren())
            _envs.Add(env.Key, env.Value!); // we're telling the compiler that environment values can never be null
    }

    public string? FindEnv(string account)
    {
        return _envs.TryGetValue(account, out var e) ? e : null;
    }
}