using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.Endpoints.Validators;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Team = Defra.Cdp.Backend.Api.Services.Entities.Model.Team;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Tests.Endpoints;

public class CdpNotifyTest
{
    /**
     * This test is to ensure that the contract between CDP Notify and Portal Backend is not broken.
     * CDP-Notify calls the /services/{id} endpoint to get team information about a given service.
     * It expects the data to be in the structure described above in CdpNotifyService.
     *
     * If this test has broken it probably means you've also broken CDP-Notify!
     */
    [Fact]
    public async Task TestCdpNotifyHasNotBeenBroken()
    {
        // Run just the Deployables API Endpoints
        var entitiesService = Substitute.For<IEntitiesService>();
        var layerService = Substitute.For<ILayerService>();
        var userServiceFetcher = Substitute.For<IUserServiceFetcher>();

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton(entitiesService);
                services.AddSingleton(layerService);
                services.AddSingleton(userServiceFetcher);
                services.AddScoped<IValidator<RequestedAnnotation>, RequestedAnnotationValidator>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => { endpoints.MapEntitiesEndpoint(); });
            });

        var server = new TestServer(builder);
        var client = server.CreateClient();

        var service = new Entity
        {
            Name = "foo",
            Status = Status.Created,
            Teams = [new Team { Name = "teamA", TeamId = "team-id-abc-123-456" }],
            Type = Type.Microservice,
            SubType = SubType.Backend,
            Created = DateTime.UtcNow,
        };

        entitiesService.GetEntity(Arg.Is("foo"), Arg.Any<CancellationToken>()).Returns(service);
        entitiesService.GetEntity(Arg.Is("missing-service"), Arg.Any<CancellationToken>()).ReturnsNull();

        var response = await client.GetAsync("entities/foo", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);

        var body = await JsonSerializer.DeserializeAsync<CdpNotifyService>(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Single(body.teams);
        Assert.Equal("team-id-abc-123-456", body.teams[0].TeamId);
        Assert.Equal("teamA", body.teams[0].Name);


        var missingResponse = await client.GetAsync("entites/unknown-service", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    /**
     * Format cdp-notify expects its data
     */
    record CdpNotifyTeam
    {
        [JsonPropertyName("teamId")] public string TeamId { get; init; }

        [JsonPropertyName("name")] public string Name { get; init; }
    }

    record CdpNotifyService
    {
        [JsonPropertyName("teams")] public List<CdpNotifyTeam> teams { get; init; }
    }
}