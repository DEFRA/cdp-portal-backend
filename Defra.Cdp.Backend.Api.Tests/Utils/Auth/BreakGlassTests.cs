using System.Security.Claims;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Utils.Auth;
using Microsoft.AspNetCore.Http;

namespace Defra.Cdp.Backend.Api.Tests.Utils.Auth;

public class BreakGlassTests
{
    [Fact]
    public void HasBreakGlass_Defaults_false()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("cdp", CdpScopes.Admin)
        ], "TestAuth"));

        Assert.False(BreakGlass.HasBreakGlass(user));
    }

    [Fact]
    public void HasBreakGlass_true_for_user()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("cdp", CdpScopes.BreakGlass)
        ], "TestAuth"));

        Assert.True(BreakGlass.HasBreakGlass(user));
    }

    [Fact]
    public void HasBreakGlass_false_for_non_owning_team()
    {
        var entity = new Entity
        {
            Name = "test-service",
            Teams = [new Team{
                TeamId = "owning_team"
            }]
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("cdp", $"{CdpScopes.BreakGlass}:team:not_owning_team")
        ], "TestAuth"));

        Assert.False(BreakGlass.HasBreakGlass(user, entity));
    }

    [Fact]
    public void HasBreakGlass_false_for_an_owning_team()
    {
        var entity = new Entity
        {
            Name = "test-service",
            Teams = [new Team{
                TeamId = "owning_team"
            }]
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("cdp", $"{CdpScopes.BreakGlass}:team:owning_team")
        ], "TestAuth"));

        Assert.True(BreakGlass.HasBreakGlass(user, entity));
    }
}