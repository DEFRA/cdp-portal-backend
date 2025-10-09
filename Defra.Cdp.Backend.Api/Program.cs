using Amazon.ECR;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.Endpoints.Validators;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Audit;
using Defra.Cdp.Backend.Api.Services.AutoDeploymentTriggers;
using Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Decommissioning;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.FeatureToggles;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.Migrations;
using Defra.Cdp.Backend.Api.Services.PlatformEvents;
using Defra.Cdp.Backend.Api.Services.PlatformEvents.Services;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Defra.Cdp.Backend.Api.Services.Shuttering;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Services.TenantStatus;
using Defra.Cdp.Backend.Api.Services.Terminal;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Defra.Cdp.Backend.Api.Utils;
using Defra.Cdp.Backend.Api.Utils.Clients;
using Defra.Cdp.Backend.Api.Utils.Logging;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Quartz;
using Serilog;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

//-------- Configure the WebApplication builder------------------//

Console.WriteLine("Testing that logs work when starting");

var builder = WebApplication.CreateBuilder(args);

// Grab environment variables
builder.Configuration.AddEnvironmentVariables("CDP");
builder.Configuration.AddEnvironmentVariables();

// Serilog
builder.Logging.ClearProviders();
builder.Services.AddHttpContextAccessor();
builder.Host.UseSerilog(CdpLogging.Configuration);

// Load certificates into Trust Store - Note must happen before Mongo and Http client connections.
builder.Services.AddCustomTrustStore();

// Add health checks and http client
builder.Services.AddHealthChecks();

builder.Services.AddHttpClient("DefaultClient", HttpClientConfiguration.Default)
    .AddHeaderPropagation();

builder.Services.AddHttpClient("ServiceClient", HttpClientConfiguration.Default);

builder.Services.AddHttpClient("GitHubClient", HttpClientConfiguration.GitHub)
    .ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>();

builder.Services.AddHttpClient("proxy", HttpClientConfiguration.Proxy)
    .ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>();


// Propagate trace header.
builder.Services.AddHeaderPropagation(options =>
{
    var traceHeader = builder.Configuration.GetValue<string>("TraceHeader");
    if (!string.IsNullOrWhiteSpace(traceHeader))
    {
        options.Headers.Add(traceHeader);
    }
});

// Handle requests behind Front Door/Load Balancer etc
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Mongo
builder.Services.AddSingleton<IMongoDbClientFactory>(_ =>
    new MongoDbClientFactory(builder.Configuration.GetValue<string>("Mongo:DatabaseUri"),
        builder.Configuration.GetValue<string>("Mongo:DatabaseName")));


// Setup the services
builder.Services.Configure<EcsEventListenerOptions>(builder.Configuration.GetSection(EcsEventListenerOptions.Prefix));
builder.Services.Configure<EcrEventListenerOptions>(builder.Configuration.GetSection(EcrEventListenerOptions.Prefix));
builder.Services.Configure<SecretEventListenerOptions>(
    builder.Configuration.GetSection(SecretEventListenerOptions.Prefix));
builder.Services.Configure<GithubWorkflowEventListenerOptions>(
    builder.Configuration.GetSection(GithubWorkflowEventListenerOptions.Prefix));
builder.Services.Configure<PlatformEventListenerOptions>(
    builder.Configuration.GetSection(PlatformEventListenerOptions.Prefix));
builder.Services.Configure<DockerServiceOptions>(builder.Configuration.GetSection(DockerServiceOptions.Prefix));
builder.Services.Configure<DeployablesClientOptions>(builder.Configuration.GetSection(DeployablesClientOptions.Prefix));
builder.Services.AddScoped<IValidator<RequestedDeployment>, RequestedDeploymentValidator>();
builder.Services.AddScoped<IValidator<RequestedAnnotation>, RequestedAnnotationValidator>();

// SQS provider


builder.Services.AddAwsClients(builder.Configuration, builder.IsDevMode());

// GitHub credential factory for the cron job
builder.Services.AddSingleton<IGithubCredentialAndConnectionFactory, GithubCredentialAndConnectionFactory>();

if (builder.IsDevMode())
{
    builder.Services.AddSingleton<IDockerCredentialProvider, EmptyDockerCredentialProvider>();
}
else
{
    builder.Services.AddSingleton<IAmazonECR, AmazonECRClient>();
    builder.Services.AddSingleton<IDockerCredentialProvider, EcrCredentialProvider>();
}

// Quartz setup for Github scheduler
builder.Services.Configure<QuartzOptions>(builder.Configuration.GetSection("Github:Scheduler"));
builder.Services.Configure<QuartzOptions>(builder.Configuration.GetSection("Decommission:Scheduler"));
builder.Services.AddQuartz(q =>
{
    var githubJobKey = new JobKey("FetchGithubRepositories");
    q.AddJob<PopulateGithubRepositories>(opts => opts.WithIdentity(githubJobKey));

    var githubInterval = builder.Configuration.GetValue<int>("Github:PollIntervalSecs");
    q.AddTrigger(opts => opts
        .ForJob(githubJobKey)
        .WithIdentity("FetchGithubRepositories-trigger")
        .WithSimpleSchedule(d => d.WithIntervalInSeconds(githubInterval).RepeatForever().Build()));

    var decommissionJobKey = new JobKey("DecommissionEntities");
    q.AddJob<DecommissioningService>(opts => opts.WithIdentity(decommissionJobKey));

    var decommissionInterval = builder.Configuration.GetValue<int>("Decommission:PollIntervalSecs");
    q.AddTrigger(opts => opts
        .ForJob(decommissionJobKey)
        .WithIdentity("DecommissionEntities-trigger")
        .WithSimpleSchedule(d => d.WithIntervalInSeconds(decommissionInterval).RepeatForever().Build()));

});
builder.Services.AddQuartzHostedService(options =>
{
    // when shutting down we want jobs to complete gracefully
    options.WaitForJobsToComplete = true;
});


// Setting up our services
builder.Services.AddSingleton<IDockerClient, DockerClient>();
builder.Services.AddSingleton<IRepositoryService, RepositoryService>();
builder.Services.AddSingleton<IDeployableArtifactsService, DeployableArtifactsService>();
builder.Services.AddSingleton<IDeploymentsService, DeploymentsService>();
builder.Services.AddSingleton<IEntitiesService, EntitiesService>();
builder.Services.AddSingleton<IEntityStatusService, EntityStatusService>();
builder.Services.AddSingleton<ILayerService, LayerService>();
builder.Services.AddSingleton<IArtifactScanner, ArtifactScanAndStore>();
builder.Services.AddSingleton<IEcrEventsService, EcrEventsService>();
builder.Services.AddSingleton<IEcsEventsService, EcsEventsService>();
builder.Services.AddSingleton<IEnvironmentLookup, EnvironmentLookup>();
builder.Services.AddSingleton<EcrEventListener>();
builder.Services.AddSingleton<EcsEventListener>();
builder.Services.AddSingleton<EcrEventHandler>();
builder.Services.AddSingleton<ITestRunService, TestRunService>();
builder.Services.AddSingleton<IAppConfigsService, AppConfigsService>();
builder.Services.AddSingleton<IGrafanaDashboardsService, GrafanaDashboardsService>();
builder.Services.AddSingleton<IAppConfigVersionsService, AppConfigVersionsService>();
builder.Services.AddSingleton<INginxVanityUrlsService, NginxVanityUrlsService>();
builder.Services.AddSingleton<INginxUpstreamsService, NginxUpstreamsService>();
builder.Services.AddSingleton<IServiceCodeCostsService, ServiceCodeCostsService>();
builder.Services.AddSingleton<ISquidProxyConfigService, SquidProxyConfigService>();
builder.Services.AddSingleton<ITenantServicesService, TenantServicesService>();
builder.Services.AddSingleton<IShutteredUrlsService, ShutteredUrlsService>();
builder.Services.AddSingleton<IEnabledVanityUrlsService, EnabledVanityUrlsService>();
builder.Services.AddSingleton<IEnabledApisService, EnabledApisService>();
builder.Services.AddSingleton<ITfVanityUrlsService, TfVanityUrlsService>();
builder.Services.AddSingleton<ITotalCostsService, TotalCostsService>();
builder.Services.AddSingleton<IVanityUrlsService, VanityUrlsService>();
builder.Services.AddSingleton<IApiGatewaysService, ApiGatewaysService>();
builder.Services.AddSingleton<ITenantStatusService, TenantStatusService>();
builder.Services.AddSingleton<ITenantRdsDatabasesService, TenantRdsDatabasesService>();

// Proxy
builder.Services.AddTransient<ProxyHttpMessageHandler>();

// Deployment Event Handlers
builder.Services.AddSingleton<TaskStateChangeEventHandler>();
builder.Services.AddSingleton<DeploymentStateChangeEventHandler>();
builder.Services.AddSingleton<LambdaMessageHandler>();
builder.Services.AddSingleton<CodeBuildStateChangeHandler>();

// Deployment Trigger Event Handlers
builder.Services.AddSingleton<AutoTestRunTriggerEventHandler>();
builder.Services.AddSingleton<IAutoDeploymentTriggerExecutor, AutoDeploymentTriggerExecutor>();

// Secret Event Handlers
builder.Services.AddSingleton<ISecretsService, SecretsService>();
builder.Services.AddSingleton<ISecretEventHandler, SecretEventHandler>();
builder.Services.AddSingleton<SecretEventListener>();

// fetchers
builder.Services.AddSingleton<SelfServiceOpsClient>();
builder.Services.AddSingleton<IUserServiceFetcher, UserServiceFetcher>();

// GitHub Workflow Event Handlers
builder.Services.AddSingleton<IGithubWorkflowEventHandler, GithubWorkflowEventHandler>();
builder.Services.AddSingleton<GithubWorkflowEventListener>();
builder.Services.AddSingleton<IPlatformEventHandler, PlatformEventHandler>();
builder.Services.AddSingleton<PlatformEventListener>();

// Pending Secrets
builder.Services.AddSingleton<IPendingSecretsService, PendingSecretsService>();

builder.Services.AddSingleton<IShutteringService, ShutteringService>();
builder.Services.AddSingleton<IShutteringArchiveService, ShutteringArchiveService>();

builder.Services.AddSingleton<IAutoDeploymentTriggerService, AutoDeploymentTriggerService>();
builder.Services.AddSingleton<IAutoTestRunTriggerService, AutoTestRunTriggerService>();

builder.Services.AddSingleton<MongoLock>();

// migrations
builder.Services.AddSingleton<IAvailableMigrations, AvailableMigrations>();
builder.Services.AddSingleton<IDatabaseMigrationService, DatabaseMigrationService>();

// Terminal Auditing
builder.Services.AddSingleton<ITerminalService, TerminalService>();

builder.Services.AddSingleton<IFeatureTogglesService, FeatureTogglesService>();
builder.Services.AddSingleton<IAuditService, AuditService>();


// Validators
// Add every validator we can find in the assembly that contains this Program
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization();

//-------- Build and Setup the WebApplication------------------//
var app = builder.Build();

app.UseRouting();
app.UseHeaderPropagation();

// enable auth
app.UseAuthentication();
app.UseAuthorization();

// Add endpoints
app.MapTenantServicesEndpoint();
app.MapTenantDatabasesEndpoint();
app.MapConfigEndpoint();
app.MapSquidProxyConfigEndpoint();
app.MapCostsEndpoint();
app.MapVanityUrlsEndpoint();
app.MapApiGatewaysEndpoint();
app.MapArtifactsAndDeployablesEndpoint();
app.MapDeploymentsEndpoint();
app.MapRepositoriesEndpoint();
app.MapEntitiesEndpoint();
app.MapTestSuiteEndpoint();
app.MapTenantSecretsEndpoint();
app.MapAdminEndpoint();
app.MapServiceStatusEndpoint();
app.MapHealthChecks("/health");
app.MapAutoDeploymentTriggerEndpoint();
app.MapAutoTestRunTriggerEndpoint();
app.MapMigrationEndpoints();
app.MapFeatureTogglesEndpoint();
app.MapShutteringEndpoint();
app.MapTerminalEndpoint();
app.MapDebugEndpoint();
app.MapAuditEndpoint();


var logger = app.Services.GetService<ILogger<Program>>();

// Start the ecs and ecr services
#pragma warning disable CS4014
var ecsSqsEventListener = app.Services.GetService<EcsEventListener>();
logger?.LogInformation("Starting ECS listener - reading service events from SQS");
Task.Run(() =>
    ecsSqsEventListener?.ReadAsync(app.Lifetime
        .ApplicationStopping)); // do not await this, we want it to run in the background

var ecrSqsEventListener = app.Services.GetService<EcrEventListener>();
logger?.LogInformation("Starting ECR listener - reading image creation events from SQS");
Task.Run(() =>
    ecrSqsEventListener?.ReadAsync(app.Lifetime
        .ApplicationStopping)); // do not await this, we want it to run in the background

var secretEventListener = app.Services.GetService<SecretEventListener>();
logger?.LogInformation("Starting Secret Event listener - reading secret update events from SQS");
Task.Run(() =>
    secretEventListener?.ReadAsync(app.Lifetime
        .ApplicationStopping)); // do not await this, we want it to run in the background

var gitHubWorkflowEventListener = app.Services.GetService<GithubWorkflowEventListener>();
logger?.LogInformation("Starting GitHub Workflow Event listener - reading workflow events from SQS");
Task.Run(() =>
    gitHubWorkflowEventListener?.ReadAsync(app.Lifetime
        .ApplicationStopping)); // do not await this, we want it to run in the background

var platformEventListener = app.Services.GetService<PlatformEventListener>();
logger?.LogInformation("Starting Platform Event listener - reading portal events from SQS");
Task.Run(() =>
    platformEventListener?.ReadAsync(app.Lifetime
        .ApplicationStopping)); // do not await this, we want it to run in the background

#pragma warning restore CS4014

BsonSerializer.RegisterSerializer(typeof(Type), new EnumSerializer<Type>(BsonType.String));
BsonSerializer.RegisterSerializer(typeof(SubType), new EnumSerializer<SubType>(BsonType.String));
BsonSerializer.RegisterSerializer(typeof(Status), new EnumSerializer<Status>(BsonType.String));
BsonSerializer.RegisterSerializer(typeof(ShutteringStatus), new EnumSerializer<ShutteringStatus>(BsonType.String));

app.Run();