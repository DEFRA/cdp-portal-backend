using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Services.Create.S3;

public class S3BucketRequest(string service, string bucketName, List<string> environments) : ICreateWorkflowRequest
{
    public GenericCdpWorkflowInputs BuildWorkflowInput(string? runId, string? useBranch, string? prTitle)
    {
        var envs = string.Join(" ", environments.Select(e => $"--environment {e}"));
        List<string> commands = [
            $"tenant s3-buckets add --service-name {service} {envs} --bucket-name {bucketName}"
        ];
        return new GenericCdpWorkflowInputs(commands, runId, useBranch, prTitle);
    }
}