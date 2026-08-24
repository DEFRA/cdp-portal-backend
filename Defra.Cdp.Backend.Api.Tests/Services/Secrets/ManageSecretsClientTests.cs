using System.Net;
using System.Text;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Secrets;

public class ManageSecretsClientTests
{
    [Fact]
    public async Task AddSecretKeyValuePair_BuildsExpectedRequestAndParsesSuccessResponse()
    {
        System.Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "test");
        System.Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "test");
        System.Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", "test-session-token");
        System.Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-2");

        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "statusCode": 200,
                      "body": {
                        "action": "add_secret_key_value_pair",
                        "secret_name": "cdp/services/cdp-portal-frontend",
                        "secret_key_pair_name": "SOME_KEY"
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"
                )
            }
        );
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("ManageSecretsClient").Returns(new HttpClient(handler));
        var options = Options.Create(
            new ManageSecretsApiOptions
            {
                BaseUrlTemplate = "https://cdp-mono-lambda.api.{environment}.cdp-int.defra.cloud"
            }
        );

        var client = new ManageSecretsClient(
            options,
            factory,
            Substitute.For<ILogger<ManageSecretsClient>>()
        );
        var result = await client.AddSecretKeyValuePair(
            "infra-dev",
            "cdp/services/cdp-portal-frontend",
            "SOME_KEY",
            "some-value",
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("add_secret_key_value_pair", result.Response!.Action);
        Assert.Equal(
            "https://cdp-mono-lambda.api.infra-dev.cdp-int.defra.cloud/secrets/add-key-value-pair",
            handler.LastRequest?.RequestUri?.ToString()
        );
    }

    [Fact]
    public async Task RemoveSecretKeyValuePair_ReturnsFailureFromBadRequest()
    {
        System.Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "test");
        System.Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "test");
        System.Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", "test-session-token");
        System.Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-2");

        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"message":"Bad request from API gateway"}""",
                    Encoding.UTF8,
                    "application/json"
                )
            }
        );
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("ManageSecretsClient").Returns(new HttpClient(handler));
        var options = Options.Create(
            new ManageSecretsApiOptions
            {
                BaseUrlTemplate = "http://localhost:3939"
            }
        );

        var client = new ManageSecretsClient(
            options,
            factory,
            Substitute.For<ILogger<ManageSecretsClient>>()
        );
        var result = await client.RemoveSecretKeyValuePair(
            "infra-dev",
            "cdp/services/cdp-portal-frontend",
            "SOME_KEY",
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("Bad request from API gateway", result.ErrorMessage);
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            LastRequest = request;
            return Task.FromResult(response);
        }
    }
}
