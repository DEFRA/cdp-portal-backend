using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Tests.Models;

public class UserServiceTests
{

    [Fact]
    public void TestDuplicateGithubTeams()
    {
        var teamA1 = new UserServiceTeams(
            "team-a",
            "",
            "cdp-team-a",
            "",
            "",
            "1234",
            []
        );
        
        var teamA2 = new UserServiceTeams(
            "team-a",
            "",
            "cdp-team-a",
            "",
            "",
            "9999",
            []
        );
        
        var user = new UserServiceRecord("", [teamA1, teamA2]);
        Assert.NotNull(user);

    }
    
}