using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Defra.Cdp.Backend.Api.Services.Secrets.events;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Secrets;

public class SecretEventHandlerTest(MongoContainerFixture fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task SecretsEventHandlerPersistsNewSecretsAndDeletesRemovedSecrets()
    {
        var mongoFactory = CreateConnectionFactory();
        var secretsService = new SecretsService(mongoFactory, new LoggerFactory());
        var secretEventHandler = new SecretEventHandler(secretsService, Substitute.For<IPendingSecretsService>(),
            new LoggerFactory().CreateLogger<SecretEventHandler>());

        var header = FromJson<SecretMessage>("""
                           {
                             "source": "cdp-secret-manager-lambda",
                             "statusCode": 200,
                             "action": "get_all_secret_keys",
                             "body": {
                               "environment": "infra-dev",
                               "secretKeys": {
                                 "cdp/services/cdp-portal-frontend": {
                                   "keys": [
                                     "TEST_KEY"
                                   ],
                                   "lastChangedDate": "2024-07-01 10:05:15",
                                   "createdDate": "2024-07-01 10:05:15"
                                 }
                               }
                             }
                           }
                           """);

        var cancellationToken = CancellationToken.None;
        await secretEventHandler.Handle(header, cancellationToken);

        var result = await secretsService.FindAllSecretsForEnvironment("infra-dev", cancellationToken);
        Assert.NotNull(result);
        Assert.Single(result);

        Assert.Equal("cdp-portal-frontend", result.First().Service);
        Assert.Equal("infra-dev", result.First().Environment);
        Assert.Single(result.First().Keys);

        Assert.Equal("TEST_KEY", result.First().Keys.First());

        header = FromJson<SecretMessage>("""
                                              {
                                                "source": "cdp-secret-manager-lambda",
                                                "statusCode": 200,
                                                "action": "get_all_secret_keys",
                                                "body": {
                                                  "environment": "infra-dev",
                                                  "secretKeys": {
                                                    "cdp/services/cdp-portal-frontend": {
                                                      "keys": [
                                                        "TEST_KEY", "TEST_KEY2"
                                                      ],
                                                      "lastChangedDate": "2024-07-01 10:05:15",
                                                      "createdDate": "2024-07-01 10:05:15"
                                                    }
                                                  }
                                                }
                                              }
                                              """);
        await secretEventHandler.Handle(header, cancellationToken);

        result = await secretsService.FindAllSecretsForEnvironment("infra-dev", cancellationToken);
        Assert.NotNull(result);
        Assert.Single(result);

        Assert.Equal("cdp-portal-frontend", result.First().Service);
        Assert.Equal("infra-dev", result.First().Environment);
        Assert.Equal(2, result.First().Keys.Count);

        Assert.Equal(["TEST_KEY", "TEST_KEY2"], result.First().Keys);

        header = FromJson<SecretMessage>("""
                                              {
                                                "source": "cdp-secret-manager-lambda",
                                                "statusCode": 200,
                                                "action": "get_all_secret_keys",
                                                "body": {
                                                  "environment": "infra-dev",
                                                  "secretKeys": {
                                                    "cdp/services/cdp-portal-frontend": {
                                                      "keys": [
                                                        "TEST_KEY", "TEST_KEY2"
                                                      ],
                                                      "lastChangedDate": "2024-07-01 10:05:15",
                                                      "createdDate": "2024-07-01 10:05:15"
                                                    },
                                                    "cdp/services/cdp-portal-backend": {
                                                        "keys": [
                                                          "TEST_KEY", "TEST_KEY4"
                                                        ],
                                                        "lastChangedDate": "2024-07-01 10:05:15",
                                                        "createdDate": "2024-07-01 10:05:15"
                                                    }
                                                  }
                                                }
                                              }
                                              """);
        await secretEventHandler.Handle(header, cancellationToken);

        result = await secretsService.FindAllSecretsForEnvironment("infra-dev", cancellationToken);
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        Assert.Equal("cdp-portal-frontend", result.First().Service);
        Assert.Equal("infra-dev", result.First().Environment);
        Assert.Equal(2, result.First().Keys.Count);

        Assert.Equal(["TEST_KEY", "TEST_KEY2"], result.First().Keys);

        Assert.Equal("cdp-portal-backend", result[1].Service);
        Assert.Equal("infra-dev", result[1].Environment);
        Assert.Equal(2, result[1].Keys.Count);

        Assert.Equal(["TEST_KEY", "TEST_KEY4"], result[1].Keys);


        header = FromJson<SecretMessage>("""
                                              {
                                                "source": "cdp-secret-manager-lambda",
                                                "statusCode": 200,
                                                "action": "get_all_secret_keys",
                                                "body": {
                                                  "environment": "infra-dev",
                                                  "secretKeys": {
                                                    "cdp/services/cdp-portal-frontend": {
                                                      "keys": [
                                                        "TEST_KEY3"
                                                      ],
                                                      "lastChangedDate": "2024-07-01 10:05:15",
                                                      "createdDate": "2024-07-01 10:05:15"
                                                    }
                                                  }
                                                }
                                              }
                                              """);
        await secretEventHandler.Handle(header, cancellationToken);

        result = await secretsService.FindAllSecretsForEnvironment("infra-dev", cancellationToken);
        Assert.NotNull(result);
        Assert.Single(result);

        Assert.Equal("cdp-portal-frontend", result.First().Service);
        Assert.Equal("infra-dev", result.First().Environment);
        Assert.Single(result.First().Keys);

        Assert.Equal(["TEST_KEY3"], result.First().Keys);
    }
}