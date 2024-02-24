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
            new()
        );
        
        var teamA2 = new UserServiceTeams(
            "team-a",
            "",
            "cdp-team-a",
            "",
            "",
            "9999",
            new()
        );
        
        var user = new UserServiceRecord("", new List<UserServiceTeams>() {teamA1, teamA2});
        Assert.NotNull(user);

    }
    
}