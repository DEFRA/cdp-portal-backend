using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Entities.LegacyHelpers;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;

namespace Defra.Cdp.Backend.Api.Tests.Services.Entities.LegacyHelpers;

public class StatusHelperTests
{
    [Fact]
    public void ReturnsCorrectListForSuccessStatus()
    {
        var result = StatusHelper.DontOverwriteStatus(Status.Success);
        Assert.Empty(result);
    }

    [Fact]
    public void ReturnsCorrectListForFailureStatus()
    {
        var result = StatusHelper.DontOverwriteStatus(Status.Failure);
        Assert.Empty(result);
    }

    [Fact]
    public void ReturnsCorrectListForInProgressStatus()
    {
        var result = StatusHelper.DontOverwriteStatus(Status.InProgress).OrderBy(x => x).ToList();
        var expected = new List<string> { Status.Success.ToStringValue(), Status.Failure.ToStringValue() }
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReturnsCorrectListForMergedStatus()
    {
        var result = StatusHelper.DontOverwriteStatus(Status.Merged).OrderBy(x => x).ToList();
        var expected = new List<string>
            {
                Status.InProgress.ToStringValue(), Status.Success.ToStringValue(), Status.Failure.ToStringValue()
            }.OrderBy(x => x)
            .ToList();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReturnsCorrectListForQueuedStatus()
    {
        var result = StatusHelper.DontOverwriteStatus(Status.Queued).OrderBy(x => x).ToList();
        var expected = new List<string>
            {
                Status.Merged.ToStringValue(),
                Status.InProgress.ToStringValue(),
                Status.Success.ToStringValue(),
                Status.Failure.ToStringValue()
            }
            .OrderBy(x => x).ToList();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReturnsCorrectListForRaisedStatus()
    {
        var result = StatusHelper.DontOverwriteStatus(Status.Raised).OrderBy(x => x).ToList();
        var expected = new List<string>
        {
            Status.Queued.ToStringValue(),
            Status.Merged.ToStringValue(),
            Status.InProgress.ToStringValue(),
            Status.Success.ToStringValue(),
            Status.Failure.ToStringValue()
        }.OrderBy(x => x).ToList();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReturnsCorrectListForRequestedStatus()
    {
        var result = StatusHelper.DontOverwriteStatus(Status.Requested).OrderBy(x => x).ToList();
        var expected = new List<string>
        {
            Status.Queued.ToStringValue(),
            Status.Merged.ToStringValue(),
            Status.InProgress.ToStringValue(),
            Status.Success.ToStringValue(),
            Status.Failure.ToStringValue()
        }.OrderBy(x => x).ToList();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReturnsCorrectListForNotRequestedStatus()
    {
        var result = StatusHelper.DontOverwriteStatus(Status.NotRequested).OrderBy(x => x).ToList();
        var expected = new List<string>
        {
            Status.Raised.ToStringValue(),
            Status.Requested.ToStringValue(),
            Status.Queued.ToStringValue(),
            Status.Merged.ToStringValue(),
            Status.InProgress.ToStringValue(),
            Status.Success.ToStringValue(),
            Status.Failure.ToStringValue()
        }.OrderBy(x => x).ToList();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetStatusKeys_ReturnsCorrectKeys_ForRepository()
    {
        var githubReposOptions = new GithubReposOptions { CdpCreateWorkflows = "workflow" };
        var statusRecord = new LegacyStatus { Kind = CreationType.Repository.ToStringValue() };

        var result = StatusHelper.GetStatusKeys(githubReposOptions, statusRecord.Kind.ToType());

        Assert.Single(result);
        Assert.Contains("workflow", result);
    }

    [Fact]
    public void GetStatusKeys_ReturnsCorrectKeys_ForMicroservice()
    {
        var githubReposOptions = new GithubReposOptions
        {
            CdpCreateWorkflows = "workflow",
            CdpNginxUpstreams = "nginx",
            CdpAppConfig = "appConfig",
            CdpTfSvcInfra = "tfSvcInfra",
            CdpSquidProxy = "squidProxy",
            CdpGrafanaSvc = "grafanaSvc"
        };
        var statusRecord = new LegacyStatus { Kind = CreationType.Microservice.ToStringValue() };

        var result = StatusHelper.GetStatusKeys(githubReposOptions, statusRecord.Kind.ToType());

        Assert.Equal(6, result.Count);
        Assert.Contains("workflow", result);
        Assert.Contains("nginx", result);
        Assert.Contains("appConfig", result);
        Assert.Contains("tfSvcInfra", result);
        Assert.Contains("squidProxy", result);
        Assert.Contains("grafanaSvc", result);
    }

    [Fact]
    public void NormaliseStatus_ReturnsSuccess_ForCompletedAndSuccess()
    {
        var result = StatusHelper.NormaliseStatus(Status.Completed, Status.Success);
        Assert.Equal(Status.Success, result);
    }

    [Fact]
    public void NormaliseStatus_ReturnsInProgress_ForCompletedAndCancelled()
    {
        var result = StatusHelper.NormaliseStatus(Status.Completed, Status.Cancelled);
        Assert.Equal(Status.InProgress, result);
    }

    [Fact]
    public void NormaliseStatus_ReturnsFailure_ForCompletedAndOther()
    {
        var result = StatusHelper.NormaliseStatus(Status.Completed, Status.Failure);
        Assert.Equal(Status.Failure, result);
    }

    [Fact]
    public void NormaliseStatus_ReturnsInProgress_ForInProgress()
    {
        var result = StatusHelper.NormaliseStatus(Status.InProgress, null);
        Assert.Equal(Status.InProgress, result);
    }

    [Fact]
    public void NormaliseStatus_ReturnsQueued_ForQueued()
    {
        var result = StatusHelper.NormaliseStatus(Status.Queued, null);
        Assert.Equal(Status.Queued, result);
    }

    [Fact]
    public void NormaliseStatus_ReturnsRequested_ForRequested()
    {
        var result = StatusHelper.NormaliseStatus(Status.Requested, null);
        Assert.Equal(Status.Requested, result);
    }

    GithubReposOptions reposOptions = new GithubReposOptions
    {
        CdpTfSvcInfra = "cdp-tf-svc-infra",
        CdpAppConfig = "cdp-app-config",
        CdpAppDeployments = "cdp-app-deployments",
        CdpCreateWorkflows = "cdp-create-workflows",
        CdpGrafanaSvc = "cdp-grafana-svc",
        CdpNginxUpstreams = "cdp-nginx-upstreams",
        CdpSquidProxy = "cdp-squid-proxy"
    };

    [Fact]
    public void Should_Provide_Success_Status_For_Microservice()
    {
        var result = StatusHelper.CalculateOverallStatus(reposOptions,
            new LegacyStatus
            {
                Kind = CreationType.Microservice.ToStringValue(),
                CdpAppConfig = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpCreateWorkflows = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpTfSvcInfra= new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpSquidProxy= new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpNginxUpstreams = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpGrafanaSvc = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                
            });
        Assert.Equal(Status.Success, result);
    }

    [Fact]
    public void Should_Provide_Failure_Status_For_Microservice()
    {
        var result = StatusHelper.CalculateOverallStatus(reposOptions,
            new LegacyStatus
            {
                Kind = CreationType.Microservice.ToStringValue(),
                CdpAppConfig = new WorkflowDetails { Status = Status.Failure.ToStringValue() },
                CdpCreateWorkflows = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpTfSvcInfra= new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpSquidProxy= new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpNginxUpstreams = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpGrafanaSvc = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                
            });
        Assert.Equal(Status.Failure, result);
    }

    [Fact]
    public void Should_Provide_In_Progress_Status_For_Microservice()
    {
        var result = StatusHelper.CalculateOverallStatus(reposOptions,
            new LegacyStatus
            {
                Kind = CreationType.Microservice.ToStringValue(),
                CdpAppConfig = new WorkflowDetails { Status = Status.Requested.ToStringValue() },
                CdpCreateWorkflows = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpTfSvcInfra= new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpSquidProxy= new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpNginxUpstreams = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpGrafanaSvc = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                
            });
        Assert.Equal(Status.InProgress, result);
    }

    [Fact]
    public void Should_Provide_Success_Status_For_Journey_Test_Suite()
    {
        var result = StatusHelper.CalculateOverallStatus(reposOptions,
            new LegacyStatus
            {
                Kind = CreationType.JourneyTestsuite.ToStringValue(),
                CdpAppConfig = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpCreateWorkflows = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpTfSvcInfra= new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpSquidProxy= new WorkflowDetails { Status = Status.Success.ToStringValue() },
                
            });
        Assert.Equal(Status.Success, result);
    }

    [Fact]
    public void Should_Provide_Failure_Status_For_Journey_Test_Suite()
    {
        var result = StatusHelper.CalculateOverallStatus(reposOptions,
            new LegacyStatus
            {
                Kind = CreationType.JourneyTestsuite.ToStringValue(),
                CdpAppConfig = new WorkflowDetails { Status = Status.Failure.ToStringValue() },
                CdpCreateWorkflows = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpTfSvcInfra= new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpSquidProxy= new WorkflowDetails { Status = Status.Success.ToStringValue() },
                
            });
        Assert.Equal(Status.Failure, result);
    }

    [Fact]
    public void Should_Provide_In_Progress_Status_For_Journey_Test_Suite()
    {
        var result = StatusHelper.CalculateOverallStatus(reposOptions,
            new LegacyStatus
            {
                Kind = CreationType.JourneyTestsuite.ToStringValue(),
                CdpAppConfig = new WorkflowDetails { Status = Status.Requested.ToStringValue() },
                CdpCreateWorkflows = new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpTfSvcInfra= new WorkflowDetails { Status = Status.Success.ToStringValue() },
                CdpSquidProxy= new WorkflowDetails { Status = Status.Success.ToStringValue() },
                
            });
        Assert.Equal(Status.InProgress, result);
    }

    [Fact]
    public void Should_Provide_Success_Status_For_Repository()
    {
        var result = StatusHelper.CalculateOverallStatus(reposOptions,
            new LegacyStatus
            {
                Kind = CreationType.Repository.ToStringValue(),
                CdpCreateWorkflows = new WorkflowDetails { Status = Status.Success.ToStringValue() },
            });
        Assert.Equal(Status.Success, result);
    }

    [Fact]
    public void Should_Provide_Failure_Status_For_Repository()
    {
        var result = StatusHelper.CalculateOverallStatus(reposOptions,
            new LegacyStatus
            {
                Kind = CreationType.Repository.ToStringValue(),
                CdpCreateWorkflows = new WorkflowDetails { Status = Status.Failure.ToStringValue() },
                
            });
        Assert.Equal(Status.Failure, result);
    }

    [Fact]
    public void Should_Provide_In_Progress_Status_For_Repository()
    {
        var result = StatusHelper.CalculateOverallStatus(reposOptions,
            new LegacyStatus
            {
                Kind = CreationType.Repository.ToStringValue(),
                CdpCreateWorkflows = new WorkflowDetails { Status = Status.Requested.ToStringValue() },
                
            });
        Assert.Equal(Status.InProgress, result);
    }
}