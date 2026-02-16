using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Handlers;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.MonoLambdaEvents.Handlers;


internal record TestPayload
{
    public required string Name { get; set; }
}

public class PlatformStateHandlerTests
{
    private readonly IEntitiesService _entitiesService = Substitute.For<IEntitiesService>();
    private readonly IUserServiceBackendClient _userServiceBackendClient = Substitute.For<IUserServiceBackendClient>();

    private const string MinimalPayload = """
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

    private const string MinimalPayloadCompressed = """
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
        var result =
            await PlatformStateHandler.DecompressAndDeserialize<TestPayload>(
                "H4sIAAAAAAAAA6tWUPJLzE1VslJQKkktLtEtS8wpTVVSqOUCAKDGhjEZAAAA");
        Assert.Equal("test-value", result.Name);
    }

    [Fact]
    public async Task TestUncompressedHandleMessage()
    {
        var handler = new PlatformStateHandler(_entitiesService, _userServiceBackendClient, new NullLoggerFactory());
        var payload = JsonSerializer.Deserialize<JsonElement>(MinimalPayload);
        await handler.HandleAsync(payload, CancellationToken.None);
        await _entitiesService.Received().UpdateEnvironmentState(Arg.Any<PlatformStatePayload>(),
            Arg.Is<Dictionary<string, UserServiceTeam>>(d => d.Count == 0),
            Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task TestCompressedPayload()
    {
        var handler = new PlatformStateHandler(_entitiesService, _userServiceBackendClient, new NullLoggerFactory());
        var payload = JsonSerializer.Deserialize<JsonElement>(MinimalPayloadCompressed);
        await handler.HandleAsync(payload, CancellationToken.None);
        await _entitiesService.Received().UpdateEnvironmentState(Arg.Any<PlatformStatePayload>(),
            Arg.Is<Dictionary<string, UserServiceTeam>>(d => d.Count == 0),
            Arg.Any<CancellationToken>());
    }
}