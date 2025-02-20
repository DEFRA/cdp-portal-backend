using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;

namespace Defra.Cdp.Backend.Api.Tests.Services.Aws.Deployments;

public class ExtractFailureReasonTests
{
    [Fact]
    public void TestExtractOutOfMemoryFailureReason()
    {
        var json = File.ReadAllText("Resources/ecs/tests/task-stop-out-of-memory.json");
        var ecsEvent = JsonSerializer.Deserialize<EcsTaskStateChangeEvent>(json);
        Assert.NotNull(ecsEvent);
        var result = TaskStateChangeEventHandler.ExtractFailureReasons(ecsEvent);

        Assert.Single(result);
        Assert.Equivalent(new FailureReason("forms-perf-test", "OutOfMemoryError: Container killed due to memory usage"), result.First());
    }
    
    [Fact]
    public void TestExtractTaskLevelFailureReason()
    {
        var json = File.ReadAllText("Resources/ecs/tests/task-stop-missing-secret.json");
        var ecsEvent = JsonSerializer.Deserialize<EcsTaskStateChangeEvent>(json);
        Assert.NotNull(ecsEvent);
        var result = TaskStateChangeEventHandler.ExtractFailureReasons(ecsEvent);

        Assert.Single(result);
        Assert.Equivalent(new FailureReason("ECS Task", "ResourceInitializationError: unable to pull secrets or registry auth: execution resource retrieval failed: unable to retrieve secret from asm: service call has been retried 1 time(s): retrieved secret from Secrets Manager did not contain json key MY_SECRET"), result.First());
    }
    
    [Fact]
    public void TestExtractTimeOutReason()
    {
        var json = File.ReadAllText("Resources/ecs/tests/task-stop-timeout-sidecar.json");
        var ecsEvent = JsonSerializer.Deserialize<EcsTaskStateChangeEvent>(json);
        Assert.NotNull(ecsEvent);
        var result = TaskStateChangeEventHandler.ExtractFailureReasons(ecsEvent);

        Assert.Single(result);
        Assert.Equivalent(new FailureReason("forms-perf-test-timeout", "Test suite exceeded maximum run time"), result.First());
    }
    
    [Fact]
    public void TestExtractNoReasonWhenTestsPass()
    {
        var json = File.ReadAllText("Resources/ecs/tests/task-stop-test-suite-pass.json");
        var ecsEvent = JsonSerializer.Deserialize<EcsTaskStateChangeEvent>(json);
        Assert.NotNull(ecsEvent);
        var result = TaskStateChangeEventHandler.ExtractFailureReasons(ecsEvent);
        
        Assert.Empty(result);
    }
    
    [Fact]
    public void TestExtractNoReasonWhenTestsFail()
    {
        var json = File.ReadAllText("Resources/ecs/tests/task-stop-test-suite-fail.json");
        var ecsEvent = JsonSerializer.Deserialize<EcsTaskStateChangeEvent>(json);
        Assert.NotNull(ecsEvent);
        var result = TaskStateChangeEventHandler.ExtractFailureReasons(ecsEvent);
        
        Assert.Empty(result);
    }
}