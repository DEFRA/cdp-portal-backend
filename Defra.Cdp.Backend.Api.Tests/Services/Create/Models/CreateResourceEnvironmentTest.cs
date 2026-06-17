using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Tests.Services.Create.Models;

public class CreateResourceEnvironmentTest
{
    [Fact]
    public void ToCdpEnvironment_maps_meta_envs()
    {
        var tenants = CreateResourceEnvironments.ToCdpEnvironments(CreateResourceEnvironments.Tenants);
        var platform = CreateResourceEnvironments.ToCdpEnvironments(CreateResourceEnvironments.Platform);
        var all =  CreateResourceEnvironments.ToCdpEnvironments(CreateResourceEnvironments.All);
        
        Assert.Equivalent(CreateResourceEnvironments.TenantEnvironments, tenants);
        Assert.Equivalent(CreateResourceEnvironments.PlatformEnvironments, platform);
        Assert.Equivalent(CdpEnvironments.Environments, all);
    } 
}