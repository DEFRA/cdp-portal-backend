using System.Text;
using Defra.Cdp.Backend.Api.Services.Create.Models;

namespace Defra.Cdp.Backend.Api.Services.Create;

public static class PrTitleBuilder
{
    public static string Build(CreateTenantResourceRequest req)
    {
        var sb = new StringBuilder("Create");
        
        if (req.S3Buckets.Length > 0)
        {
            sb.Append($" {req.S3Buckets.Length} Bucket(s)");
        }
        
        if (req.SqsQueues.Length > 0)
        {
            sb.Append($" {req.SqsQueues.Length} Queue(s)");
        }
        
        if (req.SnsTopics.Length > 0)
        {
            sb.Append($" {req.SnsTopics.Length} Topics(s)");
        }

        sb.Append($" for {string.Join(" ", req.GetServices())}");

        return sb.ToString();
    }
}