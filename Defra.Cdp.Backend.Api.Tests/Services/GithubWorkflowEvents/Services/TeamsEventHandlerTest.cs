using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Team = Defra.Cdp.Backend.Api.Services.Teams.Team;

namespace Defra.Cdp.Backend.Api.Tests.Services.GithubWorkflowEvents.Services;

public class TeamsEventHandlerTest
{

    private readonly ITeamsService _teamsService = Substitute.For<ITeamsService>();
    private readonly IUserServiceBackendClient _client = Substitute.For<IUserServiceBackendClient>();
    
    [Fact]
    public async Task Test_handler_accepts_message()
    {

        var handler = new TeamsEventHandler(_teamsService, _client, new NullLogger<TeamsEventHandler>());

        List<Team> teams = [new Team
        {
            TeamId = "foo",
            TeamName = "Foo",
            Description = "Foo Team",
            Github = "ghfoo",
            ServiceCode = "FOO"
        }];
        
        var msg = """
                  {
                    "eventType": "teams",
                    "timestamp": "2024-10-23T15:10:10.123",
                    "payload": {
                        "teams": [
                            {
                                "team_id": "foo",
                                "name": "Foo",
                                "description": "Foo Team",
                                "github": "ghfoo",
                                "service_code": "FOO"
                            }
                        ]
                    }
                  }
                  """;

        await handler.Handle(msg, TestContext.Current.CancellationToken);

        await _client.Received(1).SyncTeams(
            
            Arg.Is<IList<UserServiceTeamSync>>(actualTeams => actualTeams[0].TeamId == "foo"), 
            Arg.Any<CancellationToken>());
        await _teamsService.Received(1).SyncTeams(
            Arg.Is<IList<Team>>( actualTeams => actualTeams.SequenceEqual(teams)), 
            Arg.Any<CancellationToken>());
        
    }
}