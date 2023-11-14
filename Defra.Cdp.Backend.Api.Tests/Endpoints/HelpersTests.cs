using System.Security.Claims;
using Defra.Cdp.Backend.Api.Endpoints;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Endpoints;

public class HelpersTests
{
    [Fact]
    public void GetGroups_ReturnsGroups_WhenClaimExists()
    {
        // Arrange
        var claims = new List<Claim> { new("groups", "[\"Group1\", \"Group2\"]") };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var user = new ClaimsPrincipal(identity);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(user);

        // Act
        var groups = Helpers.ExtractGroups(httpContext);

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
        var groups = Helpers.ExtractGroups(httpContext);

        // Assert
        Assert.Null(groups);
    }
}