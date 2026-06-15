using Defra.Cdp.Backend.Api.Utils;
using static Defra.Cdp.Backend.Api.Utils.CdpEnvironments;
namespace Defra.Cdp.Backend.Api.Services.Create.Models;

public static class CreateResourceEnvironments
{
    public const string Tenant = "tenant";
    public const string Platform = "platform";
    public const string All = "all";
    
    public static readonly string[] TenantEnvironments =
    [
        Dev,
        Test,
        PerfTest,
        ExtTest,
        Prod
    ];
    
    public static readonly string[] PlatformEnvironments =
    [
        InfraDev,
        Management
    ];

    public static readonly string[] ValidCreateResourceEnvironments = 
    [
        Tenant,
        Platform,
        All,
        InfraDev,
        Management,
        Dev,
        Test,
        ExtTest,
        PerfTest,
        Prod
    ];

    /// <summary>
    /// Expands the 'meta' environments (all, tenant, platform) into arrays of actual environments 
    /// </summary>
    /// <param name="env">name of environment</param>
    /// <returns>array of cdp environment names</returns>
    public static string[] ToCdpEnvironments(string env)
    {
        if (CdpEnvironments.Environments.Contains(env))
        {
            return [env];
        }
        
        return env switch
        {
            Tenant => TenantEnvironments,
            Platform => PlatformEnvironments,
            All => CdpEnvironments.Environments,
            _ => []
        };
    }
}