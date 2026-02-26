using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Teams;

public class TeamsServiceTests(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    [Fact]
    public async Task Test_CRUD_functions()
    {
        var connectionFactory = CreateMongoDbClientFactory();
        var teamsService = new TeamsService(connectionFactory, new NullLoggerFactory());
        var team1 = new Team
        {
            TeamId = "bar",
            TeamName = "Bar",
            Description = "Bar Team",
            Github = "ghbar",
            ServiceCode = "BAR",
            SlackChannels = new SlackChannels { Prod = "bar-prod-alerts", NonProd = "bar-non-prod-alerts", Team = "bar-team" },
            Created = new DateTime()
        };

        {
            var teamsBeforeCreate = await teamsService.FindAll(TestContext.Current.CancellationToken);
            Assert.Empty(teamsBeforeCreate);
        }
        
        // Create
        {
            await teamsService.CreateTeam(team1, TestContext.Current.CancellationToken);
            var teamAfterCreate = await teamsService.Find(team1.TeamId, TestContext.Current.CancellationToken);
            Assert.Equivalent(teamAfterCreate,team1);
        }
        
        // Update
        {
            var updatedTeam1 = team1 with { Description = "updated" };
            await teamsService.UpdateTeam(updatedTeam1, TestContext.Current.CancellationToken);
            var team1AfterUpdate = await teamsService.Find(updatedTeam1.TeamId, TestContext.Current.CancellationToken);
            Assert.Equivalent(updatedTeam1, team1AfterUpdate);
        }
        
        // Delete
        {
            await teamsService.DeleteTeam(team1.TeamId, TestContext.Current.CancellationToken);
            var team1AfterDelete = await teamsService.Find(team1.TeamId, TestContext.Current.CancellationToken);
            Assert.Null(team1AfterDelete);
        }
    }

    [Fact]
    public async Task test_sync_teams_works()
    {
        var connectionFactory = CreateMongoDbClientFactory();
        var teamsService = new TeamsService(connectionFactory, new NullLoggerFactory());
        List<Team> teams = [
            new()
            {
                TeamId = "bar",
                TeamName = "Bar",
                Description = "Bar Team",
                Github = "ghbar",
                ServiceCode = "BAR",
                SlackChannels = new SlackChannels { Prod = "bar-prod-alerts", NonProd = "bar-non-prod-alerts", Team = "bar-team" },
            },
            new()
            {
                TeamId = "foo",
                TeamName = "Foo",
                Description = "Foo Team",
                Github = "ghfoo",
                ServiceCode = "FOO"
            }
        ];
        await teamsService.SyncTeams(teams, TestContext.Current.CancellationToken);
        var teamsAfterUpdate = await teamsService.FindAll(TestContext.Current.CancellationToken);
        
        Assert.Equal(2, teamsAfterUpdate.Count);
        Assert.Equivalent(teams, teamsAfterUpdate); // Expect both lists to be ordered by TeamId
    }
    
    [Fact]
    public async Task test_sync_teams_removes_missing_teams()
    {
        var connectionFactory = CreateMongoDbClientFactory();
        var teamsService = new TeamsService(connectionFactory, new NullLoggerFactory());
        List<Team> teams = [
            new()
            {
                TeamId = "bar",
                TeamName = "Bar",
                Description = "Bar Team",
                Github = "ghbar",
                ServiceCode = "BAR"
            },
            new()
            {
                TeamId = "foo",
                TeamName = "Foo",
                Description = "Foo Team",
                Github = "ghfoo",
                ServiceCode = "FOO"
            }
        ];
        
        // Insert initial teams
        {
            await teamsService.SyncTeams(teams, TestContext.Current.CancellationToken);
            var teamsAfterUpdate = await teamsService.FindAll(TestContext.Current.CancellationToken);
            Assert.Equal(2, teamsAfterUpdate.Count);
        }
        
        // Remove one team
        {
            List<Team> shorterTeamsList = [teams[0]];
            await teamsService.SyncTeams(shorterTeamsList, TestContext.Current.CancellationToken);
            var teamsAfterRemoval = await teamsService.FindAll(TestContext.Current.CancellationToken);
            Assert.Equivalent(shorterTeamsList, teamsAfterRemoval); // Expect both lists to be ordered by TeamId
        }
    }
    
    [Fact]
    public async Task test_sync_should_not_apply_empty_update()
    {
        var connectionFactory = CreateMongoDbClientFactory();
        var teamsService = new TeamsService(connectionFactory, new NullLoggerFactory());
        List<Team> teams = [
            new()
            {
                TeamId = "bar",
                TeamName = "Bar",
                Description = "Bar Team",
                Github = "ghbar",
                ServiceCode = "BAR"
            },
            new()
            {
                TeamId = "foo",
                TeamName = "Foo",
                Description = "Foo Team",
                Github = "ghfoo",
                ServiceCode = "FOO"
            }
        ];
        
        // Insert initial teams
        {
            await teamsService.SyncTeams(teams, TestContext.Current.CancellationToken);
            var teamsAfterUpdate = await teamsService.FindAll(TestContext.Current.CancellationToken);
            Assert.Equal(2, teamsAfterUpdate.Count);
        }
        
        // Bad update
        {
            var teamsBeforeUpdate = await teamsService.FindAll(TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await teamsService.SyncTeams([], TestContext.Current.CancellationToken));
            
            var teamsAfterRemoval = await teamsService.FindAll(TestContext.Current.CancellationToken);
            Assert.Equivalent(teamsBeforeUpdate, teamsAfterRemoval); // Expect both lists to be ordered by TeamId
        }
    }

}