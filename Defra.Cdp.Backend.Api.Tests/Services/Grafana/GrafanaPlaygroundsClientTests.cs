using System.Net;
using System.Text;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Grafana;
using Defra.Cdp.Backend.Api.Tests.Services.MonoLambda;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Grafana;

public class GrafanaPlaygroundsClientTests : MonoLambdaApiClientTestBase
{
    [Fact]
    public async Task GetPlaygrounds_BuildsExpectedRequest_AndParsesWrappedResponse()
    {
        var responsePayload = """
                              {"statusCode":200,"body":{"request_id":"abc-123","service":"cdp-portal-backend","dashboards":[],"alerts":[],"updated":"2026-09-01T10:00:00Z"}}
                              """;
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responsePayload, Encoding.UTF8, "application/json")
            }
        );

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("GrafanaPlaygroundsClient").Returns(new HttpClient(handler));
        var options = Options.Create(
            new MonoLambdaApiOptions
            {
                BaseUrlTemplate = "https://{restApiId}.execute-api.eu-west-2.amazonaws.com/{environment}",
                RestApiIds = [new RestApiIdMapping("dev", "abc123xyz9")]
            }
        );

        var client = new GrafanaPlaygroundsClient(
            options,
            factory,
            Substitute.For<ILogger<GrafanaPlaygroundsClient>>()
        );

        var result = await client.GetPlaygrounds("cdp-portal-backend", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("abc-123", result.Response!.RequestId);
        Assert.Equal(
            "https://abc123xyz9.execute-api.eu-west-2.amazonaws.com/dev/grafana/playgrounds/cdp-portal-backend",
            handler.LastRequest?.RequestUri?.ToString()
        );
    }

    [Fact]
    public async Task GetPlaygrounds_ParsesUnwrappedResponse()
    {
        var responsePayload = """
                              {"request_id":"abc-456","service":"cdp-uploader","dashboards":[],"alerts":[],"updated":"2026-09-01T10:00:00Z"}
                              """;
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responsePayload, Encoding.UTF8, "application/json")
            }
        );

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("GrafanaPlaygroundsClient").Returns(new HttpClient(handler));
        var options = Options.Create(
            new MonoLambdaApiOptions
            {
                BaseUrlTemplate = "http://localhost:3939",
                RestApiIds = [new RestApiIdMapping("dev", "local-stub")]
            }
        );

        var client = new GrafanaPlaygroundsClient(
            options,
            factory,
            Substitute.For<ILogger<GrafanaPlaygroundsClient>>()
        );

        var result = await client.GetPlaygrounds("cdp-uploader", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("abc-456", result.Response!.RequestId);
        Assert.Equal("cdp-uploader", result.Response.Service);
    }

    [Fact]
    public async Task GetPlaygrounds_ReturnsFailureFromBadRequest()
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
        factory.CreateClient("GrafanaPlaygroundsClient").Returns(new HttpClient(handler));
        var options = Options.Create(
            new MonoLambdaApiOptions
            {
                BaseUrlTemplate = "http://localhost:3939",
                RestApiIds = [new RestApiIdMapping("dev", "local-stub")]
            }
        );

        var client = new GrafanaPlaygroundsClient(
            options,
            factory,
            Substitute.For<ILogger<GrafanaPlaygroundsClient>>()
        );
        var result = await client.GetPlaygrounds("cdp-uploader", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("Bad request from API gateway", result.ErrorMessage);
    }
}
