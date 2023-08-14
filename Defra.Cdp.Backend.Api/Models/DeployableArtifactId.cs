namespace Defra.Cdp.Backend.Api.Models;

public sealed record DeployableArtifactId(
    string Repo,
    string Tag,
    string ServiceName,
    string? GithubUrl,
    string Sha256
);