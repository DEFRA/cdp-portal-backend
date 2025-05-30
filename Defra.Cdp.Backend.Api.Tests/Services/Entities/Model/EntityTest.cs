using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;
using Creator = Defra.Cdp.Backend.Api.Services.GithubEvents.Model.Creator;
using Team = Defra.Cdp.Backend.Api.Services.GithubEvents.Model.Team;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;
using Status = Defra.Cdp.Backend.Api.Services.Entities.Model.Status;

namespace Defra.Cdp.Backend.Api.Tests.Services.Entities.Model;

public class EntityTest
{
    [Fact]
    public void From_ReturnsEntityWithCorrectTypeAndSubType_ForJourneyTestSuite()
    {
        var legacyStatus = new LegacyStatus
        {
            Kind = "journey-testsuite",
            RepositoryName = "test-repo",
            Started = DateTime.UtcNow,
            Creator = new Creator { Id = "123", DisplayName = "Test Creator" },
            Team = new Team { TeamId = "team-1", Name = "Team One" },
            Status = "in-progress"
        };

        var result = Entity.from(legacyStatus);

        Assert.Equal(Type.TestSuite, result.Type);
        Assert.Equal(SubType.Journey, result.SubType);
        Assert.Equal("test-repo", result.Name);
        Assert.Equal(Status.Creating, result.Status);
        Assert.NotNull(result.Creator);
        Assert.NotNull(result.Teams);
        Assert.Single(result.Teams);
    }

    [Fact]
    public void From_ReturnsEntityWithCorrectTypeAndSubType_ForMicroserviceWithPublicZone()
    {
        var legacyStatus = new LegacyStatus
        {
            Kind = "microservice",
            Zone = "public",
            RepositoryName = "microservice-repo",
            Started = DateTime.UtcNow,
            Creator = new Creator { Id = "456", DisplayName = "Microservice Creator" },
            Team = new Team { TeamId = "team-2", Name = "Team Two" },
            Status = "success"
        };

        var result = Entity.from(legacyStatus);

        Assert.Equal(Type.Microservice, result.Type);
        Assert.Equal(SubType.Frontend, result.SubType);
        Assert.Equal("microservice-repo", result.Name);
        Assert.Equal(Status.Created, result.Status);
        Assert.NotNull(result.Creator);
        Assert.NotNull(result.Teams);
        Assert.Single(result.Teams);
    }

    [Fact]
    public void From_ThrowsArgumentOutOfRangeException_ForInvalidStatus()
    {
        var legacyStatus = new LegacyStatus
        {
            Kind = "repository",
            RepositoryName = "invalid-status-repo",
            Status = "unknown-status",
            Creator = new Creator { Id = "456", DisplayName = "Microservice Creator" },
            Team = new Team { TeamId = "team-2", Name = "Team Two" }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => Entity.from(legacyStatus));
    }

    [Fact]
    public void From_ThrowsArgumentOutOfRangeException_ForUnknownKind()
    {
        var legacyStatus = new LegacyStatus
        {
            Kind = "unknown-kind",
            RepositoryName = "unknown-kind-repo",
            Status = "failed",
            Creator = new Creator { Id = "456", DisplayName = "Microservice Creator" },
            Team = new Team { TeamId = "team-2", Name = "Team Two" }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => Entity.from(legacyStatus));
    }
}