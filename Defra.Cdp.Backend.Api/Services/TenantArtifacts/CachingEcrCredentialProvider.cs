using Amazon.ECR;
using Amazon.ECR.Model;
using Defra.Cdp.Backend.Api.Config;

namespace Defra.Cdp.Backend.Api.Services.TenantArtifacts;

public static class DockerCredentialProvider
{
    public static void AddDockerCredentialProvider(this WebApplicationBuilder builder)
    {
        if (builder.IsDevMode())
        {
            builder.Services.AddSingleton<IDockerCredentialProvider, EmptyDockerCredentialProvider>();
        }
        else
        {
            builder.Services.AddSingleton<IAmazonECR, AmazonECRClient>();
            builder.Services.AddSingleton<IDockerCredentialProvider, EcrCredentialProvider>();
        }
    }
}

public interface IDockerCredentialProvider
{
    Task<string?> GetCredentials();
}

public class CachingEcrCredentialProvider : IDockerCredentialProvider
{
    private readonly IAmazonECR ecrClient;
    private readonly ILogger logger;
    private AuthorizationData? cachedAuthData;


    public CachingEcrCredentialProvider(IAmazonECR ecrClient, ILogger<CachingEcrCredentialProvider> logger)
    {
        this.ecrClient = ecrClient;
        this.logger = logger;
    }

    public async Task<string?> GetCredentials()
    {
        if (cachedAuthData != null)
            logger.LogInformation("ECR token expires at: {ExpireAt}, expired: utc {ExpiredTime}",
                cachedAuthData.ExpiresAt,
                cachedAuthData.ExpiresAt.AddMinutes(5).CompareTo(DateTime.UtcNow) <= 0);

        if (cachedAuthData == null || cachedAuthData.ExpiresAt.AddMinutes(5).CompareTo(DateTime.UtcNow) <= 0)
        {
            logger.LogInformation("Renewing docker credentials from ECR");
            var resp = await ecrClient.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
            if (resp == null || resp.AuthorizationData.Count == 0)
            {
                logger.LogInformation("Failed to get ECR credentials");
                throw new Exception("Failed to get ECR credentials");
            }

            cachedAuthData = resp.AuthorizationData[0];
            logger.LogInformation("Got ECS auth token, expires at {ExpiresAt}", cachedAuthData.ExpiresAt);
        }

        return cachedAuthData.AuthorizationToken;
    }
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
        var resp = await ecrClient.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
        if (resp == null || resp.AuthorizationData.Count == 0)
        {
            logger.LogInformation("Failed to get ECR credentials");
            throw new Exception("Failed to get ECR credentials");
        }

        logger.LogInformation("Got ECS auth token, expires at {ExpiresAt}", resp.AuthorizationData[0].ExpiresAt);

        return resp.AuthorizationData[0].AuthorizationToken;
    }
}