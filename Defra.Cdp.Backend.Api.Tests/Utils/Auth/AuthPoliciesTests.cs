using System.Security.Claims;
using Defra.Cdp.Backend.Api.Utils.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.Cdp.Backend.Api.Tests.Utils.Auth;

public class AuthPoliciesTests
{
   
    private readonly IPolicyEvaluator _evaluator;
    private readonly IServiceProvider _services;

    public AuthPoliciesTests()
    {
        // Setup minimal services for the evaluator
        var services = new ServiceCollection();
        services.AddAuthorization();
        services.AddLogging();
        services.AddOptions();
        _services = services.BuildServiceProvider();
        _evaluator = _services.GetRequiredService<IPolicyEvaluator>();
    }

    [Fact]
    public async Task IsAdmin_UserWithAdminClaim_Succeeds()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("cdp", "permission:admin")
        ], "TestAuth"));

        var context = new DefaultHttpContext { User = user };

        var result = await _evaluator.AuthenticateAsync(AuthPolicies.IsAdmin, context);
        var authResult = await _evaluator.AuthorizeAsync(AuthPolicies.IsAdmin, result, context, resource: null);

        Assert.True(authResult.Succeeded);
    }

    [Fact]
    public async Task IsAdmin_UserWithoutClaim_Fails()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("cdp", "permission:tenant")
        ], "TestAuth"));

        var context = new DefaultHttpContext { User = user };

        var result = await _evaluator.AuthenticateAsync(AuthPolicies.IsAdmin, context);
        var authResult = await _evaluator.AuthorizeAsync(AuthPolicies.IsAdmin, result, context, resource: null);

        Assert.False(authResult.Succeeded);
    }
    
    [Fact]
    public async Task IsAdmin_NoUser_Fails()
    {
        var context = new DefaultHttpContext {};

        var result = await _evaluator.AuthenticateAsync(AuthPolicies.IsAdmin, context);
        var authResult = await _evaluator.AuthorizeAsync(AuthPolicies.IsAdmin, result, context, resource: null);

        Assert.False(authResult.Succeeded);
    }
    
    [Fact]
    public async Task IsTenant_TenantUser_Succeeds()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("cdp", "permission:tenant")
        ], "TestAuth"));

        var context = new DefaultHttpContext { User = user };

        var result = await _evaluator.AuthenticateAsync(AuthPolicies.IsTenant, context);
        var authResult = await _evaluator.AuthorizeAsync(AuthPolicies.IsTenant, result, context, resource: null);

        Assert.True(authResult.Succeeded);
    }
    
    [Fact]
    public async Task IsTenant_AdminUser_Succeeds()
    {
        // Admin users are also valid
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("cdp", "permission:admin")
        ], "TestAuth"));

        var context = new DefaultHttpContext { User = user };

        var result = await _evaluator.AuthenticateAsync(AuthPolicies.IsTenant, context);
        var authResult = await _evaluator.AuthorizeAsync(AuthPolicies.IsTenant, result, context, resource: null);

        Assert.True(authResult.Succeeded);
    }
    
    [Fact]
    public async Task IsTenant_NonTenant_Fails()
    {
        // Admin users are also valid
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("cdp", "permission:somethingelse")
        ], "TestAuth"));

        var context = new DefaultHttpContext { User = user };

        var result = await _evaluator.AuthenticateAsync(AuthPolicies.IsTenant, context);
        var authResult = await _evaluator.AuthorizeAsync(AuthPolicies.IsTenant, result, context, resource: null);

        Assert.False(authResult.Succeeded);
    }

    [Fact]
    public async Task IsTenant_NoUser_Fails()
    {
        var context = new DefaultHttpContext();

        var result = await _evaluator.AuthenticateAsync(AuthPolicies.IsTenant, context);
        var authResult = await _evaluator.AuthorizeAsync(AuthPolicies.IsTenant, result, context, resource: null);

        Assert.False(authResult.Succeeded);
    }
}