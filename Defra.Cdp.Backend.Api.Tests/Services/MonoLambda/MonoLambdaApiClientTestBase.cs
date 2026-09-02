namespace Defra.Cdp.Backend.Api.Tests.Services.MonoLambda;

public abstract class MonoLambdaApiClientTestBase
{
    protected MonoLambdaApiClientTestBase()
    {
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "test");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "test");
        Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", "test-session-token");
        Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-2");
    }
}

public sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
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
