using System.Security.Claims;
using Defra.Cdp.Backend.Api.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Endpoints;

public class HelpersTests
{
    private readonly ILoggerFactory _loggerFactory = new LoggerFactory();


    [Fact]
    public void GetGroups_ReturnsGroups_WhenClaimExists()
    {
        // Arrange
        var claims = new List<Claim> { new("groups", "Group1"), new("groups", "Group2") };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var user = new ClaimsPrincipal(identity);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(user);

        // Act
        var groups = Helpers.ExtractGroups(httpContext, _loggerFactory)!;

        // Assert
        Assert.Equal(2, groups.Count);
        Assert.Contains("Group1", groups);
        Assert.Contains("Group2", groups);
    }

    [Fact]
    public void GetGroups_ReturnsEmptyList_WhenNoClaimExists()
    {
        // Arrange
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(new ClaimsPrincipal());

        // Act
        var groups = Helpers.ExtractGroups(httpContext, _loggerFactory);

        // Assert
        Assert.Null(groups);
    }
}