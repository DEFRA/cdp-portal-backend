using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Handlers;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.MonoLambda.Handlers;

public class SecretUpdatesHandlerTests
{
    
    private readonly ISecretsService _secretsService = Substitute.For<ISecretsService>();

    
    [Fact]
    public async Task Test_secrets_are_updated()
    {
        var testMessage = """
                           {
                            "event_type": "get_all_secret_keys",
                            "get_all_secret_keys": true,
                            "secretKeys": {
                              "cdp/services/my-service": {
                                "keys": ["KEY_ONE", "KEY_TWO"],
                                "lastChangedDate": "2024-01-01T12:00:00+00:00",
                                "createdDate": "2024-01-01T10:00:00+00:00"
                              }   
                            },  
                            "exception": "", 
                            "environment": "infra-dev",
                            "timestamp": "2024-01-01T12:00:00+00:00"
                          }
                          """;
        
        _secretsService.FindAllSecretsForEnvironment(Arg.Is("infra-dev"), Arg.Any<CancellationToken>())
            .Returns([]);
        
        var handler = new SecretUpdatesHandler(_secretsService, NullLogger<SecretUpdatesHandler>.Instance);
        var msg = JsonSerializer.Deserialize<JsonElement>(testMessage);
        await handler.HandleAsync(msg, TestContext.Current.CancellationToken);


        await _secretsService.DidNotReceiveWithAnyArgs()
            .DeleteSecrets(Arg.Any<List<TenantSecrets>>(), Arg.Any<CancellationToken>());
        
        await _secretsService.Received().UpdateSecrets(
            Arg.Is<List<TenantSecrets>>(l => l.Count == 1 && l[0].Environment == "infra-dev" && l[0].Service == "my-service"  ), 
            Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task Test_deleted_secrets_are_removed()
    {
        var testMessage = """
                           {
                            "event_type": "get_all_secret_keys",
                            "get_all_secret_keys": true,
                            "secretKeys": {
                              "cdp/services/my-service": {
                                "keys": ["KEY_ONE", "KEY_TWO"],
                                "lastChangedDate": "2024-01-01T12:00:00+00:00",
                                "createdDate": "2024-01-01T10:00:00+00:00"
                              }   
                            },  
                            "exception": "", 
                            "environment": "infra-dev",
                            "timestamp": "2024-01-01T12:00:00+00:00"
                          }
                          """;
        
        _secretsService.FindAllSecretsForEnvironment(Arg.Is("infra-dev"), Arg.Any<CancellationToken>())
            .Returns([new TenantSecrets { Environment = "infra-dev", Service = "foo" }]);
        
        var handler = new SecretUpdatesHandler(_secretsService, NullLogger<SecretUpdatesHandler>.Instance);
        var msg = JsonSerializer.Deserialize<JsonElement>(testMessage);
        await handler.HandleAsync(msg, TestContext.Current.CancellationToken);

        await _secretsService.Received()
            .DeleteSecrets(Arg.Is<List<TenantSecrets>>(l => l[0].Service == "foo" && l.Count == 1), Arg.Any<CancellationToken>());

        
        await _secretsService.Received().UpdateSecrets(
            Arg.Is<List<TenantSecrets>>(l => l.Count == 1 && l[0].Environment == "infra-dev" && l[0].Service == "my-service"  ), 
            Arg.Any<CancellationToken>());
    }
}