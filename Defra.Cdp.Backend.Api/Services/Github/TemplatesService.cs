using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.Github;

public interface ITemplatesService
{
    Task<IEnumerable<Repository>> AllTemplates();

    Task<IEnumerable<Repository>> FindTemplatesByTeam(string team);

    Task<Repository?> FindTemplateById(string id);

    ServiceTypesResult AllServiceTypes();
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

    public async Task<IEnumerable<Repository>> AllTemplates()
    {
        var availableTemplates =
            _templatesFromConfig.Templates.Select(t => _repositoryService.FindRepositoryById(t.Key));
        var repos = await Task.WhenAll(availableTemplates);
        return repos.Where(r => r != null).Select(r => r!);
    }

    public async Task<IEnumerable<Repository>> FindTemplatesByTeam(string team)
    {
        var repositories = await AllTemplates();
        return repositories.Where(r => r.Teams.Any(t => t.GithubId == team));
    }

    public async Task<Repository?> FindTemplateById(string id)
    {
        return await _repositoryService.FindRepositoryById(id);
    }

    public ServiceTypesResult AllServiceTypes()
    {
        return new ServiceTypesResult("success", _templatesFromConfig.Templates.Select(t =>
            new ServiceType(t.Key, t.Value)
        ));
    }
}