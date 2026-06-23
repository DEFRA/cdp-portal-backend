using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Endpoints;

public class ResourceRequestStatusEndpointTest
{
    [Fact]
    public async Task Should_return_resource_request_status_with_pull_request_details()
    {
        var requestService = Substitute.For<IResourceRequestService>();
        var requestId = ObjectId.GenerateNewId();
        requestService.GetByEntityAndId("foo-service", requestId, Arg.Any<CancellationToken>())
            .Returns(new ResourceRequestRecord
            {
                Id = requestId,
                EntityName = "foo-service",
                RequestedAt = DateTime.UtcNow,
                Inputs = new GenericCdpWorkflowInputs(["tenant s3-buckets add --service-name foo-service"], "run-123", "tenant-request-run-123", "title"),
                Workflow = new GitHubTriggerWorkflowResponse
                {
                    WorkflowRunId = 123456789,
                    WorkflowRunUrl = "https://api.github.com/repos/DEFRA/cdp-tenant-config/actions/runs/123456789",
                    WorkflowRunHtmlUrl = "https://github.com/DEFRA/cdp-tenant-config/actions/runs/123456789"
                },
                PullRequest = new ResourceRequestPullRequest
                {
                    Url = "https://github.com/DEFRA/cdp-tenant-config/pull/77",
                    Number = 77
                }
            });

        using var host = await BuildHost(requestService);
        var client = host.GetTestClient();

        var response =
            await client.GetAsync($"/entities/foo-service/resource-requests/{requestId}",
                TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(requestId.ToString(), json.RootElement.GetProperty("requestId").GetString());
        Assert.Equal("run-123", json.RootElement.GetProperty("runId").GetString());
        Assert.Equal("tenant-request-run-123", json.RootElement.GetProperty("branch").GetString());
        Assert.Equal("https://github.com/DEFRA/cdp-tenant-config/pull/77",
            json.RootElement.GetProperty("pullRequest").GetProperty("url").GetString());
    }

    [Fact]
    public async Task Should_return_bad_request_for_invalid_request_id()
    {
        var requestService = Substitute.For<IResourceRequestService>();
        using var host = await BuildHost(requestService);
        var client = host.GetTestClient();

        var response =
            await client.GetAsync("/entities/foo-service/resource-requests/not-an-object-id",
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<IHost> BuildHost(IResourceRequestService requestService)
    {
        return await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthorization();
                    services.AddSingleton(requestService);
                    services.AddSingleton(Substitute.For<IEntitiesService>());
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.Use(async (context, next) =>
                    {
                        context.User = new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim("cdp", "permission:admin")],
                            "TestAuth"));
                        await next();
                    });
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => { endpoints.MapEntitiesEndpoint(); });
                });
            })
            .StartAsync(TestContext.Current.CancellationToken);
    }
}
