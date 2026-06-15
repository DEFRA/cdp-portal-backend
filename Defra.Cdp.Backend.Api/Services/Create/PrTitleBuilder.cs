using System.Text;
using Defra.Cdp.Backend.Api.Services.Create.Models;

namespace Defra.Cdp.Backend.Api.Services.Create;

public static class PrTitleBuilder
{
    public static string Build(CreateTenantResourceRequest req)
    {
        var sb = new StringBuilder("Tenant: Create");
        
        if (req.S3Buckets.Count > 0)
        {
            sb.Append($" {req.S3Buckets.Count} S3,");
        }
        
        if (req.SqsQueues.Count > 0)
        {
            sb.Append($" {req.SqsQueues.Count} SQS,");
        }
        
        if (req.SnsTopics.Count > 0)
        {
            sb.Append($" {req.SnsTopics.Count} SNS,");
        }

        sb.Append($" for {string.Join(",", req.GetServices())}");

        return sb.ToString();
    }
}