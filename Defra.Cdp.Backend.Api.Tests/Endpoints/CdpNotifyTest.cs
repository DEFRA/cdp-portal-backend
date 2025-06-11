using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.Endpoints.Validators;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

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
    public async Task TestCdpNotifyHasntBeenBroken()
    {
        // Run just the Deployables API Endpoints
        var deployableArtifactsService = Substitute.For<IDeployableArtifactsService>();
        var layerService = Substitute.For<ILayerService>();
        var userServiceFetcher = Substitute.For<IUserServiceFetcher>();
        
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton(deployableArtifactsService);
                services.AddSingleton(layerService);
                services.AddSingleton(userServiceFetcher);
                services.AddScoped<IValidator<RequestedAnnotation>, RequestedAnnotationValidator>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapDeployablesEndpoint();
                });
            });

        // Create Server
        var server = new TestServer(builder);
        var client = server.CreateClient();

        // Setup Mocks.
        var service = new ServiceInfo("foo", "https://github.com/DEFRA/foo", "foo",
            new List<RepositoryTeam> { new("teamAGitHub", "teamA1234", "teamA") });
        deployableArtifactsService.FindServices(Arg.Is("foo"), Arg.Any<CancellationToken>()).Returns( service );
        deployableArtifactsService.FindServices(Arg.Is("missing-service"), Arg.Any<CancellationToken>()).ReturnsNull();
        
        var response = await client.GetAsync("services/foo");

        Assert.True(response.IsSuccessStatusCode);
        
        var body = await JsonSerializer.DeserializeAsync<CdpNotifyService>(await response.Content.ReadAsStreamAsync());
        Assert.NotNull(body);
        Assert.Single(body.teams);
        Assert.Equal("teamA1234", body.teams[0].TeamId);
        Assert.Equal("teamA", body.teams[0].Name);
        
        
        var missingResponse = await client.GetAsync("services/unknown-service");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }
    
    /**
     * Format cdp-notify expects its data
     */
    record CdpNotifyTeam
    {
        [JsonPropertyName("teamId")]
        public string TeamId { get; init; }
    
        [JsonPropertyName("name")]
        public string Name { get; init; }

    }
    record CdpNotifyService
    {
        [JsonPropertyName("teams")]
        public List<CdpNotifyTeam> teams { get; init; }
    }
}