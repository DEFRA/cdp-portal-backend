using System.Collections.Immutable;

namespace Defra.Cdp.Backend.Api.Services.Github;

public class TemplatesFromConfig
{
    public readonly ImmutableDictionary<string, string> Templates;

    public TemplatesFromConfig(IConfiguration cfg)
    {
        var section = cfg.GetSection("TemplateMappings");
        Templates = section.GetChildren().Select(s => new KeyValuePair<string, string>(s.Key, s.Value!))
            .ToImmutableDictionary();
    }

    public string? FindTemplate(string templateName)
    {
        return Templates.GetValueOrDefault(templateName);
    }
}