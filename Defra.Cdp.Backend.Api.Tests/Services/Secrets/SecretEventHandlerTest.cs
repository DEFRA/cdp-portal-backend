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
        var pendingSecretsService =  Substitute.For<IPendingSecretsService>();
        var eventHandler = new SecretEventHandler(service, pendingSecretsService,
            ConsoleLogger.CreateLogger<SecretEventHandler>());
        
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
        var body =
            @"{""source"": ""cdp-secret-manager-lambda"", ""statusCode"": 200, ""action"": ""get_all_secret_keys"", ""body"": {}}";
       var res = SecretEventHandler.TryParseMessageHeader(body);
       Assert.NotNull(res);
    }
    
    [Fact]
    public void TryParseMessageHeaderInvalid()
    {
        var otherLambda =
            @"{""source"": ""cdp-some-other-lambda"", ""statusCode"": 200, ""action"": ""get_all_secret_keys"", ""body"": {}}";
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
        var body =
            @"{""source"": ""cdp-secret-manager-lambda"", ""statusCode"": 200, ""action"": ""get_all_secret_keys"",
                ""body"":
                   {
                       ""get_all_secret_keys"": true,
                        ""exception"": """",
                        ""environment"": ""dev"",
                        ""secretKeys"": {
                            ""cdp/services/service-1"": {
                                ""createdDate"": ""2024-01-01T00:00:00"",
                                ""keys"": [""key1"", ""key2""],
                                ""lastChangedDate"": ""2022-01-01T00:00:00""
                            },
                            ""cdp/services/service-2"": {
                                ""createdDate"": ""2024-02-01T00:00:00"",
                                ""keys"": [""key3"", ""key4""],
                                ""lastChangedDate"": ""2023-01-01T00:00:00""
                            },
                            ""cdp/services/service-3"": {
                                ""createdDate"": ""2021-01-01T00:00:00"",
                                ""keys"": [""key5"", ""key6""],
                                ""lastChangedDate"": ""2023-02-01T00:00:00""
                            }
                        }
                    }
            }";
        var res = SecretEventHandler.TryParseMessageHeader(body);
        Assert.NotNull(res);

        var parsedBody = res.Body.Deserialize<BodyGetAllSecretKeys>();
        Assert.Equal("dev", parsedBody?.Environment);
        Assert.Equal("", parsedBody?.Exception);
        Assert.Equal(3, parsedBody?.SecretKeys.Count);

        Assert.Equal("2024-01-01T00:00:00", parsedBody?.SecretKeys["cdp/services/service-1"].CreatedDate);
        Assert.Equal("2022-01-01T00:00:00", parsedBody?.SecretKeys["cdp/services/service-1"].LastChangedDate);
        Assert.Equal(new [] { "key1", "key2" }, parsedBody?.SecretKeys["cdp/services/service-1"].Keys);

        Assert.Equal("2024-02-01T00:00:00", parsedBody?.SecretKeys["cdp/services/service-2"].CreatedDate);
        Assert.Equal("2023-01-01T00:00:00", parsedBody?.SecretKeys["cdp/services/service-2"].LastChangedDate);
        Assert.Equal(new [] { "key3", "key4" }, parsedBody?.SecretKeys["cdp/services/service-2"].Keys);

        Assert.Equal("2021-01-01T00:00:00", parsedBody?.SecretKeys["cdp/services/service-3"].CreatedDate);
        Assert.Equal("2023-02-01T00:00:00", parsedBody?.SecretKeys["cdp/services/service-3"].LastChangedDate);
        Assert.Equal(new [] { "key5", "key6" }, parsedBody?.SecretKeys["cdp/services/service-3"].Keys);
    }
}