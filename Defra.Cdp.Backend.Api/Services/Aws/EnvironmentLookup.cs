using System.Security.Cryptography;
using System.Text;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface IEnvironmentLookup
{
    string? FindEnv(string account);
    string? FindAccount(string env);
    string? FindS3BucketSuffix(string env);

}

public class EnvironmentLookup : IEnvironmentLookup
{
    private readonly Dictionary<string, string> _envs = new();
    private readonly Dictionary<string, string> _accounts = new();
    private readonly Dictionary<string, string> _bucketSuffixes = new();

    public EnvironmentLookup(IConfiguration cfg)
    {
        var section = cfg.GetSection("EnvironmentMappings");
        foreach (var env in section.GetChildren())
        {
            if (env.Value == null) continue;
            _envs[env.Key] = env.Value;
            _accounts[env.Value] = env.Key;
        }
        
        var s3Section = cfg.GetSection("BucketSuffixes");
        foreach (var s3 in s3Section.GetChildren())
        {
            if (s3.Value == null) continue;
            _bucketSuffixes[s3.Key] = s3.Value;
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

    public string? FindS3BucketSuffix(string env)
    {
        return _bucketSuffixes.TryGetValue(env, out var suffix) ? suffix : null;
    }
}