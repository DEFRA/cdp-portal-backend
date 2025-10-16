using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Handlers;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.MonoLambdaEvents.Handlers;


internal record TestPayload
{
    public string Name { get; set; }
}

public class PlatformStateHandlerTests
{
    private IEntitiesService entitiesService = Substitute.For<IEntitiesService>();

    private string minimalPayload = 
        """
        { 
            "payload_version": "11111111111111111",
            "payload": {
                "created": "2025-10-14T15:20:03.747Z",
                "version": 1,
                "environment": "dev",
                "terraform_serials": {
                  "tfsvcinfra": 1,
                  "tfvanityurl": 2,
                  "tfwaf": 3,
                  "tfopensearch": 4,
                  "tfgrafana": 5
                },
                "tenants": {}
            },
            "compression": null
        }
        """;
    
    private string minimalPayloadCompressed = 
        """
        { 
            "payload_version": "11111111111111111",
            "payload": "H4sIAAAAAAAAAz2OwQ6CMBBE73wF4SymLRATvsOTF7OBrTaBrdnWGkP4d7eoHOfN7MwuRVlWAyNEHKu+rIwyXa1Vrduz7nqjetUcT+3pUh1yMCEH50mCetNIybGnGSnm4xHTNxeRGazn+RqQHUxB3EWMbNmQBkeW4d+ywQTk4vvJk1Cz0xdY0c2u/QMpIPBwF9zu+CZjQLmwE7T+XiCguA2vxVp8ABqBllDoAAAA",
            "compression": "gzip",
            "encoding": "base64"
        }
        """;

    
    [Fact]
    public async Task TestDecompressAndDeserialize()
    {
        // Test data generated via: echo -n '{ "Name": "test-value" }' | gzip | base64 -w 0
        var result = await PlatformStateHandler.DecompressAndDeserialize<TestPayload>("H4sIAAAAAAAAA6tWUPJLzE1VslJQKkktLtEtS8wpTVVSqOUCAKDGhjEZAAAA");
        Assert.Equal("test-value", result.Name);
    }

    [Fact]
    public async Task TestUncompressedHandleMessage()
    {
        var handler = new PlatformStateHandler(entitiesService, new NullLoggerFactory());
        var payload = JsonSerializer.Deserialize<JsonElement>(minimalPayload);
        await handler.HandleAsync(payload, CancellationToken.None);
        await entitiesService.Received().UpdateEnvironmentState(Arg.Any<PlatformStatePayload>(), Arg.Any<CancellationToken>());
    }
    
    
    [Fact]
    public async Task TestCompressedPayload()
    {
        var handler = new PlatformStateHandler(entitiesService, new NullLoggerFactory());
        var payload = JsonSerializer.Deserialize<JsonElement>(minimalPayloadCompressed);
        await handler.HandleAsync(payload, CancellationToken.None);
        await entitiesService.Received().UpdateEnvironmentState(Arg.Any<PlatformStatePayload>(), Arg.Any<CancellationToken>());
    }
}