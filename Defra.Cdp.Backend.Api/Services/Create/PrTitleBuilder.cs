using System.Text;
using Defra.Cdp.Backend.Api.Services.Create.Models;

namespace Defra.Cdp.Backend.Api.Services.Create;

public static class PrTitleBuilder
{
    public static string Build(CreateTenantResourceRequest req)
    {
        var sb = new StringBuilder("Tenant: Create");
        
        if (req.S3Buckets.Length > 0)
        {
            sb.Append($" {req.S3Buckets.Length} S3,");
        }
        
        if (req.SqsQueues.Length > 0)
        {
            sb.Append($" {req.SqsQueues.Length} SQS,");
        }
        
        if (req.SnsTopics.Length > 0)
        {
            sb.Append($" {req.SnsTopics.Length} SNS,");
        }

        sb.Append($" for {string.Join(",", req.GetServices())}");

        return sb.ToString();
    }
}