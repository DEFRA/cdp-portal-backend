using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Defra.Cdp.Backend.Api.Services.Secrets.events;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Secrets;

public class SecretEventHandlerTest
{

    [Fact]
    public async void WillProcessGetAllSecretsPayload()
    {
        var service =  Substitute.For<ISecretsService>();
        var eventHandler = new SecretEventHandler(service, ConsoleLogger.CreateLogger<SecretEventHandler>());
        
        var mockPayload = SecretEventHandler.TryParseMessageHeader(await File.ReadAllTextAsync("Resources/payload-get-all-secrets.json"));

        Assert.NotNull(mockPayload);
        service
            .UpdateSecrets(Arg.Any<List<TenantSecrets>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await eventHandler.Handle(mockPayload, new CancellationToken());

        await service.ReceivedWithAnyArgs().UpdateSecrets(Arg.Any<List<TenantSecrets>>(), Arg.Any<CancellationToken>());

    }
    
    [Fact]
    public void TryParseMessageHeaderWithValidPayload()
    {
       var body = "{\"source\": \"cdp-secret-manager-lambda\", \"statusCode\": 200, \"action\": \"get_all_secret_keys\", \"body\": {}}";
       var res = SecretEventHandler.TryParseMessageHeader(body);
       Assert.NotNull(res);
    }
    
    [Fact]
    public void TryParseMessageHeaderInvalid()
    {
        var otherLambda = "{\"source\": \"cdp-some-other-lambda\", \"statusCode\": 200, \"action\": \"get_all_secret_keys\", \"body\": {}}";
        var res = SecretEventHandler.TryParseMessageHeader(otherLambda);
        Assert.Null(res);
        
        var otherMessage = "{\"foo\": \"bar\"}";
        res = SecretEventHandler.TryParseMessageHeader(otherMessage);
        Assert.Null(res);

        var invalidJson = "<tag>foo</tag>";
        res = SecretEventHandler.TryParseMessageHeader(invalidJson);
        Assert.Null(res);
    }

    [Fact]
    public void CanExtractBody()
    {
        var body = "{\"source\": \"cdp-secret-manager-lambda\", \"statusCode\": 200, \"action\": \"get_all_secret_keys\", \"body\": " +
                   "{ \"get_all_secret_keys\": true, " +
                   "\"exception\": \"\", " +
                   "\"environment\": \"dev\", " +
                   "\"keys\": {\"cdp/service/foo\": [\"FOO\"]}" +
                   "}}";
        var res = SecretEventHandler.TryParseMessageHeader(body);
        Assert.NotNull(res);

        var parsedBody = res.Body.Deserialize<BodyGetAllSecretKeys>();
        Assert.Equal("dev", parsedBody?.Environment);
        Assert.Equal("", parsedBody?.Exception);
        Assert.Equal(1, parsedBody?.Keys.Count);
    }
}