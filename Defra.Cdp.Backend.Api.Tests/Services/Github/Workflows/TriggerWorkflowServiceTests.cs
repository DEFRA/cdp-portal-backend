using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Github.Workflows;

public class TriggerWorkflowServiceTests
{
    [Fact]
    public async Task ReturnsNullWhenDispatchReturnsNoContentBody()
    {
        var service = BuildService(_ => new HttpResponseMessage(HttpStatusCode.NoContent)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var response = await service.TriggerWorkflow("cdp-deployments-snow", "deploy.yml",
            new TestInputs("value"), TestContext.Current.CancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task ReturnsDeserialisedResponseWhenBodyPresent()
    {
        var service = BuildService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "workflow_run_id": 99,
                  "run_url": "https://api.github.com/repos/DEFRA/cdp-deployments-snow/actions/runs/99",
                  "html_url": "https://github.com/DEFRA/cdp-deployments-snow/actions/runs/99"
                }
                """,
                Encoding.UTF8,
                "application/json")
        });

        var response = await service.TriggerWorkflow("cdp-deployments-snow", "deploy.yml",
            new TestInputs("value"), TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal(99, response.WorkflowRunId);
    }

    private static TriggerWorkflowService BuildService(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var handler = new StubMessageHandler(responseFactory);
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("GitHubClient").Returns(httpClient);

        var credentials = Substitute.For<IGithubCredentialAndConnectionFactory>();
        credentials.GetToken(Arg.Any<CancellationToken>()).Returns("token");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Github:ApiUrl"] = "https://api.github.com",
                ["Github:Organisation"] = "DEFRA"
            })
            .Build();

        return new TriggerWorkflowService(httpClientFactory, credentials, config,
            NullLogger<TriggerWorkflowService>.Instance);
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class TestInputs(string value) : IGithubWorkflowInputs
    {
        [JsonPropertyName("value")]
        public string Value { get; init; } = value;
    }
}
