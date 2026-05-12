using System.Security.Claims;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Utils.Auth;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Defra.Cdp.Backend.Api.Tests.Utils.Auth;

public class EntityOwnerFilterTests
{
    [Fact]
    public async Task EntityOwnerFilter_OwnerOfEntity_Succeeds()
    {
        var service = new Entity
        {
            Name = "foo-backend",
            Teams = [new Team { Name = "Foo", TeamId = "foo" }],
        };
        var entitiesService = Substitute.For<IEntitiesService>();
        entitiesService.GetEntity(Arg.Is("foo-backend"), Arg.Any<CancellationToken>()).Returns(service);

        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockServiceProvider.GetService(typeof(IEntitiesService)).Returns(entitiesService);
        
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "1234"),
            new Claim("cdp", "permission:serviceOwner:team:foo")
        ], "TestAuth"));

        var ctx = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext {
            User = user,
            RequestServices = mockServiceProvider
        });

        var filter = new EntityOwnerFilter(_ => "foo-backend");
        var result = await filter.InvokeAsync(ctx, x => new ValueTask<object?>(x));
        
        Assert.NotNull(result);
        Assert.IsNotType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(result);

        var resultCtx = (EndpointFilterInvocationContext)result;
        Assert.Equal(200, resultCtx.HttpContext.Response.StatusCode);
    }
    
    [Fact]
    public async Task EntityOwnerFilter_OwnerOfEntity_WithMultipleTeams_succeeds()
    {
        var service = new Entity
        {
            Name = "foo-backend",
            Teams = [
                new Team { Name = "Platform", TeamId = "platform" },
                new Team { Name = "Foo", TeamId = "foo" }
            ]
        };
        
        var entitiesService = Substitute.For<IEntitiesService>();
        entitiesService.GetEntity(Arg.Is("foo-backend"), Arg.Any<CancellationToken>()).Returns(service);

        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockServiceProvider.GetService(typeof(IEntitiesService)).Returns(entitiesService);
        
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "1234"),
            new Claim("cdp", "permission:serviceOwner:team:foo")
        ], "TestAuth"));

        var ctx = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext {
            User = user,
            RequestServices = mockServiceProvider
        });

        var filter = new EntityOwnerFilter(_ => "foo-backend");
        var result = await filter.InvokeAsync(ctx, x => new ValueTask<object?>(x));
        
        Assert.NotNull(result);
        Assert.IsNotType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(result);

        var resultCtx = (EndpointFilterInvocationContext)result;
        Assert.Equal(200, resultCtx.HttpContext.Response.StatusCode);
    }
    
    [Fact]
    public async Task EntityOwnerFilter_Admin_Succeeds()
    {
        var service = new Entity
        {
            Name = "foo-backend",
            Teams = [new Team { Name = "Foo", TeamId = "foo" }],
        };
        var entitiesService = Substitute.For<IEntitiesService>();
        entitiesService.GetEntity(Arg.Is("foo-backend"), Arg.Any<CancellationToken>()).Returns(service);

        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockServiceProvider.GetService(typeof(IEntitiesService)).Returns(entitiesService);
        
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "1234"),
            new Claim("cdp", "permission:admin")
        ], "TestAuth"));

        var ctx = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext {
            User = user,
            RequestServices = mockServiceProvider
        });

        var filter = new EntityOwnerFilter(_ => "foo-backend");
        var result = await filter.InvokeAsync(ctx, x => new ValueTask<object?>(x));
        
        Assert.NotNull(result);
        Assert.IsNotType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(result);

        var resultCtx = (EndpointFilterInvocationContext)result;
        Assert.Equal(200, resultCtx.HttpContext.Response.StatusCode);
    }
    
    [Fact]
    public async Task EntityOwnerFilter_ownerNotInTeam_fails()
    {
        var service = new Entity
        {
            Name = "foo-backend",
            Teams = [new Team { Name = "Foo", TeamId = "foo" }],
        };
        
        var entitiesService = Substitute.For<IEntitiesService>();
        entitiesService.GetEntity(Arg.Is("foo-backend"), Arg.Any<CancellationToken>()).Returns(service);

        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockServiceProvider.GetService(typeof(IEntitiesService)).Returns(entitiesService);
        
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "1234"),
            new Claim("cdp", "permission:serviceOwner:team:bar")
        ], "TestAuth"));
        
        var ctx = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext {
            User = user,
            RequestServices = mockServiceProvider
        });

        var filter = new EntityOwnerFilter(_ => "foo-backend");
        var result = await filter.InvokeAsync(ctx, x => new ValueTask<object?>(x));
        Assert.NotNull(result);
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(result);
    }
    
    [Fact]
    public async Task EntityOwnerFilter_NotLoggedIn_Fails()
    {
        var service = new Entity
        {
            Name = "foo-backend",
            Teams = [new Team { Name = "Foo", TeamId = "foo" }],
        };
        
        var entitiesService = Substitute.For<IEntitiesService>();
        entitiesService.GetEntity(Arg.Is("foo-backend"), Arg.Any<CancellationToken>()).Returns(service);

        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockServiceProvider.GetService(typeof(IEntitiesService)).Returns(entitiesService);

        var ctx = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext {
            RequestServices = mockServiceProvider
        });

        var filter = new EntityOwnerFilter(_ => "foo-backend");
        var result = await filter.InvokeAsync(ctx, x => new ValueTask<object?>(x));
        Assert.NotNull(result);
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(result);
    }
    
    [Fact]
    public async Task EntityOwnerFilter_InvalidEntity_Fails()
    {
        var entitiesService = Substitute.For<IEntitiesService>();
        entitiesService.GetEntity(Arg.Is("foo-backend"), Arg.Any<CancellationToken>()).ReturnsNull();

        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockServiceProvider.GetService(typeof(IEntitiesService)).Returns(entitiesService);

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "1234"),
            new Claim("cdp", "permission:serviceOwner:team:foo")
        ], "TestAuth"));
        
        var ctx = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext {
            User = user,
            RequestServices = mockServiceProvider
        });

        var filter = new EntityOwnerFilter(_ => "foo-backend");
        var result = await filter.InvokeAsync(ctx, x => new ValueTask<object?>(x));
        Assert.NotNull(result);
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(result);
    }
}