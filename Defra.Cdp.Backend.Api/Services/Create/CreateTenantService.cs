
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Utils.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using EntityTeam = Defra.Cdp.Backend.Api.Services.Entities.Model.Team;

namespace Defra.Cdp.Backend.Api.Services.Create;


public record CreateTenantServiceRequest
{
    public required string Name { get; init; }
    public required string TeamId { get; init; }
    public required string TemplateId { get; init; }
    public string? TemplateTag { get; init; }
}

public record TenantTemplate
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Language { get; init; }
    public required string TemplateRepository { get; init; }
    public string? TemplateBranch { get; init; }
    public required string Zone { get; init; }
    public required bool Mongo { get; init; }
    public required bool Redis { get; init; }
    public string? RequiredScope { get; init; }
    public required Entities.Model.Type EntityType { get; init; }
    public required Entities.Model.SubType EntitySubType { get; init; }
}

public record CreateTenantWorkflowInputs
{
    [property: JsonPropertyName("service")]
    public required string Service { get; set; }
    
    [property: JsonPropertyName("template_repo")]
    public required string TemplateRepo { get; set; }
    
    [property: JsonPropertyName("config")] 
    public required String Config { get; set; } // stringified CreateTenantWorkflowInputsConfig

    public static CreateTenantWorkflowInputs Build(string service, string templateRepo, TenantTemplate template, UserServiceTeam team)
    {
        var config = new CreateTenantWorkflowInputsConfig
        {
            Zone = template.Zone,
            MongoEnabled = template.Mongo,
            RedisEnabled = template.Redis,
            ServiceCode = team.serviceCode.FirstOrDefault(),
            SubType = template.EntitySubType,
            Type = template.EntityType,
            Team = team.github!
        };
        return new CreateTenantWorkflowInputs
        {
            Service = service,
            TemplateRepo = templateRepo,
            Config = JsonSerializer.Serialize(config)
        };
    }
}

public record CreateTenantWorkflowInputsConfig
{

    [property: JsonPropertyName("zone")]
    public required string Zone { get; set; }
    
    [property: JsonPropertyName("mongo_enabled")]
    public required bool MongoEnabled { get; set; }
    
    [property: JsonPropertyName("redis_enabled")]
    public required bool RedisEnabled { get; set; }
    
    [property: JsonPropertyName("type")]
    public required Entities.Model.Type Type { get; set; }

    [property: JsonPropertyName("subtype")]
    public required Entities.Model.SubType SubType { get; set; }

    [property: JsonPropertyName("team")]
    public required string Team { get; set; }
    
    [property: JsonPropertyName("service_code")]
    public string? ServiceCode { get; set; }
}

public class CreateTenantService(IEntitiesService entitiesService, IAuthorizationService authorizationService, UserServiceBackendClient usbClient)
{
    private Dictionary<string, TenantTemplate> templates = new Dictionary<string, TenantTemplate>();
    
    public async void Create(CreateTenantServiceRequest request, HttpContext context)
    {
        // TODO: replace with call to USB
        UserServiceTeam team = new UserServiceTeam("platform", "this is test data", "platform", "", "", "platform", [], ["CDP"]);
        if (team.github == null)
        {
            throw new Exception("Team is not linked to a GitHub team");
        }

        var userId = context.User.GetObjectId();
        var user = await usbClient.GetUser(userId!, context.RequestAborted);

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => ctx.User.HasClaim(c => c.Type == "cdp_scope" && c.Value == "team:" + ctx.Resource.ToString()))
            .Build();
        await authorizationService.AuthorizeAsync(context.User, team.teamId, policy);
        
        var template = templates[request.TemplateId];
        // Check template is valid and user has permissions

        var entity = new Entity
        {
            PrimaryLanguage = template.Language,
            Name = request.Name,
            Type = template.EntityType,
            SubType = template.EntitySubType,
            Teams = [new EntityTeam { Name = team.name, TeamId = team.teamId }],
            Status = Status.Creating,
            Creator = new UserDetails{ DisplayName = user.name, Id = userId}
        };
        await entitiesService.Create(entity, context.RequestAborted);
        
        // Build GitHub payload
        var branchOrTag = request.TemplateTag ?? template.TemplateBranch ?? "main";
        var templateRepo = $"{template.TemplateRepository}@{branchOrTag}";
        var inputs = CreateTenantWorkflowInputs.Build(request.Name, templateRepo, template, team);
        
        // TODO: inject GitHub client and trigger workflow with params
    }

    public async Task<List<string>> ListTemplates(HttpContext context)
    {
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new OwnerOfService())
            .Build();
        
        var res = await authorizationService.AuthorizeAsync(context.User, "platform" );
        Console.WriteLine($"AUTH result{res.Succeeded} or {res.Failure}");

        return new List<string>();
    }
}

public class OwnerOfServiceAuthorizationHandler : 
    AuthorizationHandler<OwnerOfService, string>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OwnerOfService requirement,
        string teamId)
    {
        if (context.User.HasClaim(c => c.Value == $"permission:serviceOwner:team:{teamId}"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class OwnerOfService : IAuthorizationRequirement {}

public class AllowedToUseTemplateAuthorizationHandler : 
    AuthorizationHandler<AllowedToUserTemplate, TenantTemplate>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        AllowedToUserTemplate requirement,
        TenantTemplate template)
    {
        if (template.RequiredScope == null || context.User.HasClaim(c => c.Value == template.RequiredScope))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class AllowedToUserTemplate : IAuthorizationRequirement {}

