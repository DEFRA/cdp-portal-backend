namespace Defra.Cdp.Backend.Api.Config;

public class DockerServiceOptions
{
    public const string Prefix = "Docker";
    public string RegistryUrl { get; set; } = string.Empty;
}