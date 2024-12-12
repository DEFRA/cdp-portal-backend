using Amazon.ECR;
using Amazon.ECR.Model;

namespace Defra.Cdp.Backend.Api.Services.TenantArtifacts;

public interface IDockerCredentialProvider
{
    Task<string?> GetCredentials();
}

public class EmptyDockerCredentialProvider : IDockerCredentialProvider
{
    public Task<string?> GetCredentials()
    {
        return Task<string?>.Factory.StartNew(() => null);
    }
}

// a non-caching version for debugging purposes
public class EcrCredentialProvider : IDockerCredentialProvider
{
    private readonly IAmazonECR ecrClient;
    private readonly ILogger logger;


    public EcrCredentialProvider(IAmazonECR ecrClient, ILogger<EcrCredentialProvider> logger)
    {
        this.ecrClient = ecrClient;
        this.logger = logger;
    }

    public async Task<string?> GetCredentials()
    {
        logger.LogInformation("Renewing docker credentials from ECR.");
        try
        {
            var resp = await ecrClient.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
            if (resp == null || resp.AuthorizationData.Count == 0)
            {
                logger.LogInformation("Failed to get ECR credentials");
                throw new Exception("Failed to get ECR credentials");
            }

            logger.LogInformation("Got ECS auth token, expires at {ExpiresAt}", resp.AuthorizationData[0].ExpiresAt);

            return resp.AuthorizationData[0].AuthorizationToken;
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to get docker login {ex}", ex);
            throw;
        }
    }
}