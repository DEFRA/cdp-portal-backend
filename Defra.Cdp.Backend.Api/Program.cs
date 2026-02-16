using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.Endpoints.Validators;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Schedulers;
using Defra.Cdp.Backend.Api.Services.Audit;
using Defra.Cdp.Backend.Api.Services.AutoDeploymentTriggers;
using Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Decommissioning;
using Defra.Cdp.Backend.Api.Services.Dependencies;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.EventHistory;
using Defra.Cdp.Backend.Api.Services.FeatureToggles;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.Migrations;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Handlers;
using Defra.Cdp.Backend.Api.Services.PlatformEvents;
using Defra.Cdp.Backend.Api.Services.PlatformEvents.Services;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Defra.Cdp.Backend.Api.Services.Shuttering;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Services.Users;
using Defra.Cdp.Backend.Api.Services.Teams;
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
using Quartz.Simpl;
using Quartz.Spi;
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

// Setup Bson Serializers ahead of any mongo services (This will cause problems if the enum is used in an index)
BsonSerializer.RegisterSerializer(typeof(Type), new EnumSerializer<Type>(BsonType.String));
BsonSerializer.RegisterSerializer(typeof(SubType), new EnumSerializer<SubType>(BsonType.String));
BsonSerializer.RegisterSerializer(typeof(Status), new EnumSerializer<Status>(BsonType.String));
BsonSerializer.RegisterSerializer(typeof(ShutteringStatus), new EnumSerializer<ShutteringStatus>(BsonType.String));


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
builder.Services.Configure<DeployablesClientOptions>(builder.Configuration.GetSection(DeployablesClientOptions.Prefix));
builder.Services.Configure<CloudWatchMetricsOptions>(builder.Configuration.GetSection(CloudWatchMetricsOptions.Prefix));
builder.Services.AddScoped<IValidator<RequestedDeployment>, RequestedDeploymentValidator>();


// AWS Clients
builder.Services.AddAwsClients(builder.Configuration, builder.IsDevMode());

// GitHub related services
builder.Services.AddSingleton<IGithubCredentialAndConnectionFactory, GithubCredentialAndConnectionFactory>();
builder.Services.AddTransient<PopulateGithubRepositories>();
builder.Services.AddTransient<RepositoryCreationPoller>();
builder.Services.AddTransient<DecommissioningService>();
builder.Services.AddSingleton<IJobFactory, MicrosoftDependencyInjectionJobFactory>();

// Quartz setup for GitHub schedulers
builder.Services.AddHostedService<QuartzSchedulersHostedService>();

// Setting up our services
builder.Services.AddSingleton<IRepositoryService, RepositoryService>();
builder.Services.AddSingleton<IDeployableArtifactsService, DeployableArtifactsService>();
builder.Services.AddSingleton<IDeploymentsService, DeploymentsService>();
builder.Services.AddSingleton<IEntitiesService, EntitiesService>();
builder.Services.AddSingleton<IEcrEventsService, EcrEventsService>();
builder.Services.AddSingleton<IEcsEventsService, EcsEventsService>();
builder.Services.AddSingleton<IEnvironmentLookup, EnvironmentLookup>();
builder.Services.AddSingleton<EcrEventListener>();
builder.Services.AddSingleton<EcsEventListener>();
builder.Services.AddSingleton<EcrEventHandler>();
builder.Services.AddSingleton<ITestRunService, TestRunService>();
builder.Services.AddSingleton<IAppConfigsService, AppConfigsService>();
builder.Services.AddSingleton<IAppConfigVersionsService, AppConfigVersionsService>();
builder.Services.AddSingleton<IServiceCodeCostsService, ServiceCodeCostsService>();
builder.Services.AddSingleton<ITotalCostsService, TotalCostsService>();

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
builder.Services.AddSingleton<ISelfServiceOpsClient, SelfServiceOpsClient>();
builder.Services.AddSingleton<IUserServiceBackendClient, UserServiceBackendClient>();

// GitHub Workflow Event Handlers
builder.Services.AddSingleton<GithubWorkflowEventListener>();
builder.Services.AddSingleton<IPlatformEventHandler, PlatformEventHandler>();
builder.Services.AddSingleton<PlatformEventListener>();
builder.Services.AddSingleton<IGithubWorkflowEventHandler, AppConfigsService>();
builder.Services.AddSingleton<IGithubWorkflowEventHandler, AppConfigVersionsService>();
builder.Services.AddSingleton<IGithubWorkflowEventHandler, TeamsEventHandler>();
builder.Services.AddSingleton<IGithubWorkflowEventHandler, UsersEventHandler>();

// Pending Secrets
builder.Services.AddSingleton<IPendingSecretsService, PendingSecretsService>();

builder.Services.AddSingleton<IShutteringService, ShutteringService>();
builder.Services.AddSingleton<IShutteringArchiveService, ShutteringArchiveService>();

builder.Services.AddSingleton<IAutoDeploymentTriggerService, AutoDeploymentTriggerService>();
builder.Services.AddSingleton<IAutoTestRunTriggerService, AutoTestRunTriggerService>();

builder.Services.AddSingleton<MongoLock>();
builder.Services.AddSingleton<ITeamsService, TeamsService>();
builder.Services.AddSingleton<IUsersService, UsersService>();

// migrations
builder.Services.AddSingleton<IAvailableMigrations, AvailableMigrations>();
builder.Services.AddSingleton<IDatabaseMigrationService, DatabaseMigrationService>();

// Terminal Auditing
builder.Services.AddSingleton<ITerminalService, TerminalService>();

builder.Services.AddSingleton<IFeatureTogglesService, FeatureTogglesService>();
builder.Services.AddSingleton<IAuditService, AuditService>();
builder.Services.AddSingleton<ICloudWatchMetricsService, CloudWatchMetricsService>();

// New Tenant state stuff
builder.Services.Configure<LambdaEventListenerOptions>(builder.Configuration.GetSection(LambdaEventListenerOptions.Prefix));
builder.Services.AddSingleton<MonoLambdaEventListener>();
builder.Services.AddSingleton<IMonoLambdaEventHandler, PlatformStateHandler>();
builder.Services.AddSingleton<IEventHistoryFactory, EventHistoryFactory>();

// SBOM deployment pusher
if (builder.IsDevMode())
{
    builder.Services.AddSingleton<ISbomExplorerClient, NoOpSbomExplorerClient>();   
}
else
{
    builder.Services.AddSingleton<ISbomExplorerClient, SbomExplorerClient>();
}
builder.Services.AddSingleton<ISbomEcrEventHandler, SbomEcrEventHandler>();
builder.Services.AddSingleton<ISbomDeploymentEventHandler, SbomDeploymentEventHandler>();

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
app.MapRepositoriesEndpoint();
app.MapConfigEndpoint();
app.MapCostsEndpoint();
app.MapArtifactsAndDeployablesEndpoint();
app.MapDeploymentsEndpoint();
app.MapEntitiesEndpoint();
app.MapTestSuiteEndpoint();
app.MapTenantSecretsEndpoint();
app.MapAdminEndpoint();
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

var lambdaEventListener = app.Services.GetService<MonoLambdaEventListener>();
logger?.LogInformation("Starting Lambda Event listener - reading portal events from SQS");
Task.Run(() =>
    lambdaEventListener?.ReadAsync(app.Lifetime
        .ApplicationStopping)); // do not await this, we want it to run in the background

#pragma warning restore CS4014

app.Run();