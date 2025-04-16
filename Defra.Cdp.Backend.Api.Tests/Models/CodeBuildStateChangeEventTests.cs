using System.Text.Json;
using System.Text.Json.Nodes;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Tests.Models;

public class CodeBuildStateChangeEventTests
{
    [Fact]
    public void TestJsonDeserialization()
    {
        var json = File.ReadAllText("Resources/codebuild/codebuildstatechange.json");
        var codeBuildEvent = JsonSerializer.Deserialize<CodeBuildStateChangeEvent>(json);

        Assert.NotNull(codeBuildEvent);
        Assert.Equal("0000", codeBuildEvent.Account);
        Assert.Equal("SUCCEEDED", codeBuildEvent.Detail.BuildStatus);
    }
}