using System.Security.Cryptography;
using System.Text;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface IEnvironmentLookup
{
    string? FindEnv(string account);
}

public class EnvironmentLookup : IEnvironmentLookup
{
    private readonly Dictionary<string, string> _envs = new();
    private readonly Dictionary<string, string> _accounts = new();

    public EnvironmentLookup(IConfiguration cfg)
    {
        var section = cfg.GetSection("EnvironmentMappings");
        foreach (var env in section.GetChildren())
        {
            if (env.Value == null) continue;
            _envs.Add(env.Key, env.Value);
            _accounts.Add(env.Value, env.Key);
        }
    }

    public string? FindEnv(string account)
    {
        return _envs.TryGetValue(account, out var env) ? env : null;
    }

    public string? FindAccount(string env)
    {
        return _accounts.TryGetValue(env, out var account) ? account : null;
    }
}