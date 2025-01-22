using System.Collections.Immutable;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.Github;

public interface ITemplatesService
{
    Task<IEnumerable<Repository>> AllTemplates(CancellationToken cancellationToken);

    Task<IEnumerable<Repository>> FindTemplatesByTeam(string team, CancellationToken cancellationToken);

    Task<Repository?> FindTemplateById(string id, CancellationToken cancellationToken);

    ServiceTypesResult AllServiceTemplates();
}

public class TemplatesService : ITemplatesService
{
    private readonly ILogger<TemplatesService> _logger;
    private readonly IRepositoryService _repositoryService;
    private readonly TemplatesFromConfig _templatesFromConfig;

    public TemplatesService(IRepositoryService repositoryService, ILoggerFactory loggerFactory,
        IConfiguration cfg)
    {
        _templatesFromConfig = new TemplatesFromConfig(cfg);
        _logger = loggerFactory.CreateLogger<TemplatesService>();
        _repositoryService = repositoryService;
    }

    public async Task<IEnumerable<Repository>> AllTemplates(CancellationToken cancellationToken)
    {
        var availableTemplates =
            _templatesFromConfig._templates.Select(t => _repositoryService.FindRepositoryById(t.Repository, cancellationToken));
        var repos = await Task.WhenAll(availableTemplates);
        return repos.Where(r => r != null).Select(r => r!);
    }

    public async Task<IEnumerable<Repository>> FindTemplatesByTeam(string team, CancellationToken cancellationToken)
    {
        var repositories = await AllTemplates(cancellationToken);
        return repositories.Where(r => r.Teams.Any(t => t.Github == team));
    }

    public async Task<Repository?> FindTemplateById(string id, CancellationToken cancellationToken)
    {
        return await _repositoryService.FindRepositoryById(id, cancellationToken);
    }

    public ServiceTypesResult AllServiceTemplates()
    {
        var keyValuePairs = _templatesFromConfig._templates.Select(template =>
            new KeyValuePair<string, ServiceType>(template.Repository, new ServiceType(template))
        );
        return new ServiceTypesResult("success", keyValuePairs.ToImmutableDictionary());
    }
}