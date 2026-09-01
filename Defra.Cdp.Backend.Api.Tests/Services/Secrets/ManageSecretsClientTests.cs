using System.Net;
using System.Text;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Defra.Cdp.Backend.Api.Tests.Services.MonoLambda;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Secrets;

public class ManageSecretsClientTests : MonoLambdaApiClientTestBase
{
    [Fact]
    public async Task AddSecretKeyValuePair_BuildsExpectedRequestAndBuildsSuccessResponse()
    {
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"statusCode": 200, "body": null}""",
                    Encoding.UTF8,
                    "application/json"
                )
            }
        );
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("ManageSecretsClient").Returns(new HttpClient(handler));
        var options = Options.Create(
            new MonoLambdaApiOptions
            {
                BaseUrlTemplate = "https://{restApiId}.execute-api.eu-west-2.amazonaws.com/{environment}",
                RestApiIds = [new RestApiIdMapping("infra-dev", "abc123xyz9")]
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
        Assert.Equal("cdp/services/cdp-portal-frontend", result.Response!.SecretName);
        Assert.Equal("SOME_KEY", result.Response!.SecretKeyPairName);
        Assert.Equal(
            "https://abc123xyz9.execute-api.eu-west-2.amazonaws.com/infra-dev/secrets/add-key-value-pair",
            handler.LastRequest?.RequestUri?.ToString()
        );
    }

    [Fact]
    public async Task AddSecretKeyValuePair_ThrowsWhenRestApiIdNotConfiguredForEnvironment()
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("ManageSecretsClient").Returns(new HttpClient(handler));
        var options = Options.Create(
            new MonoLambdaApiOptions
            {
                BaseUrlTemplate = "https://{restApiId}.execute-api.eu-west-2.amazonaws.com/{environment}"
            }
        );

        var client = new ManageSecretsClient(
            options,
            factory,
            Substitute.For<ILogger<ManageSecretsClient>>()
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.AddSecretKeyValuePair(
            "dev",
            "cdp/services/cdp-portal-frontend",
            "SOME_KEY",
            "some-value",
            CancellationToken.None
        ));
    }

    [Fact]
    public async Task RemoveSecretKeyValuePair_ReturnsFailureFromBadRequest()
    {
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
            new MonoLambdaApiOptions
            {
                BaseUrlTemplate = "http://localhost:3939",
                RestApiIds = [new RestApiIdMapping("infra-dev", "local-stub")]
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
}
