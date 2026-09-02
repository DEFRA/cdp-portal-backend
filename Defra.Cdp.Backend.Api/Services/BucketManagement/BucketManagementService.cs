using Defra.Cdp.Backend.Api.Services.BucketManagement.Models;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement;

public interface IBucketManagementService
{
    Task<List<BucketObject>> GetBucketResources(String bucket, String path, CancellationToken cancellationToken);
}

public class BucketManagementService() : IBucketManagementService
{
    public async Task<List<BucketObject>> GetBucketResources(String bucket, String path, CancellationToken cancellationToken)
    {
        return [];
    }
}