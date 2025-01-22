
namespace Defra.Cdp.Backend.Api.Services.Github;

public class TemplatesFromConfig
{
    public readonly List<ServiceTemplate> _templates = new();

    public TemplatesFromConfig(IConfiguration cfg)
    {
        var section = cfg.GetSection("TemplateMappings");
        foreach (var templateSection in section.GetChildren())
        {
            _templates.Add(new ServiceTemplate(
                templateSection.Key,
                templateSection.GetSection("Description").Value!,
                templateSection.GetSection("Language").Value!,
                templateSection.GetSection("RequiredScope").Value,
                templateSection.GetSection("Type").Value!,
                templateSection.GetSection("Zone").Value!
            ));
        }
    }

}

public record ServiceTemplate(string Repository, string Description, string Language, string? RequiredScope, string Type, string Zone);
