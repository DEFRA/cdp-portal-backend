using Amazon.ECR;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.Endpoints.Validators;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Services.Tenants;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Identity.Web;
using Quartz;
using Serilog;
using Serilog.Extensions.Logging;

//-------- Configure the WebApplication builder------------------//

Console.WriteLine("Testing that logs work when starting");

var builder = WebApplication.CreateBuilder(args);

// Grab environment variables
builder.Configuration.AddEnvironmentVariables("CDP");
builder.Configuration.AddEnvironmentVariables();

// Serilog
builder.Logging.ClearProviders();
var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Logging.AddSerilog(logger);

Console.WriteLine("Logger created.");

logger.Information("Starting CDP Portal Backend, bootstrapping the services");

// Add health checks and http client
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();

builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

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
builder.Services.Configure<DockerServiceOptions>(builder.Configuration.GetSection(DockerServiceOptions.Prefix));
builder.Services.Configure<DeployablesClientOptions>(builder.Configuration.GetSection(DeployablesClientOptions.Prefix));
builder.Services.AddScoped<IValidator<RequestedDeployment>, RequestedDeploymentValidator>();

// SQS provider
logger.Information("Attempting to add SQS, ECR and Docker Client");
builder.Services.AddSqsClient(builder.Configuration, builder.IsDevMode());

// Github credential factory for the cron job
builder.Services.AddSingleton<IGithubCredentialAndConnectionFactory, GithubCredentialAndConnectionFactory>();

if (builder.IsDevMode())
{
    logger.Information("Using mock Docker Credential Provider");
    builder.Services.AddSingleton<IDockerCredentialProvider, EmptyDockerCredentialProvider>();
}
else
{
    logger.Information("Connecting to Amazon ECR");
    builder.Services.AddSingleton<IAmazonECR, AmazonECRClient>();
    logger.Information("Connecting to ECR as a docker registry");
    builder.Services.AddSingleton<IDockerCredentialProvider, EcrCredentialProvider>();
    logger.Information("Setting up Github App credential provider");
    builder.Services.AddSingleton<IGithubCredentialAndConnectionFactory, GithubCredentialAndConnectionFactory>();
}


// Quartz setup for Github scheduler
builder.Services.Configure<QuartzOptions>(builder.Configuration.GetSection("Github:Scheduler"));
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("FetchGithubRepositories");
    q.AddJob<PopulateGithubRepositories>(opts => opts.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("FetchGithubRepositories-trigger")
        .WithCronSchedule("0 0/1 * * * ?"));
});
builder.Services.AddQuartzHostedService(options =>
{
    // when shutting down we want jobs to complete gracefully
    options.WaitForJobsToComplete = true;
});

// Setting up our services
builder.Services.AddSingleton<IDockerClient, DockerClient>();
builder.Services.AddSingleton<IRepositoryService, RepositoryService>();
builder.Services.AddSingleton<IDeployablesService, DeployablesService>();
builder.Services.AddSingleton<IDeploymentsService, DeploymentsService>();
builder.Services.AddSingleton<ILayerService, LayerService>();
builder.Services.AddSingleton<IArtifactScanner, ArtifactScanner>();
builder.Services.AddSingleton<IEcrEventsService, EcrEventsService>();
builder.Services.AddSingleton<IEcsEventsService, EcsEventsService>();
builder.Services.AddSingleton<EnvironmentLookup>();
builder.Services.AddSingleton<EcrEventListener>();
builder.Services.AddSingleton<EcsEventListener>();
builder.Services.AddSingleton<TemplatesFromConfig>();
builder.Services.AddSingleton<ITemplatesService, TemplatesService>();


// Validators
// Add every validator we can find in the assembly that contains this Program
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddEndpointsApiExplorer();
if (builder.IsDevMode()) builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization();

//-------- Build and Setup the WebApplication------------------//
var app = builder.Build();

// Create swagger doc from internal endpoints then add the swagger ui endpoint
// Under `Endpoints` directory, the `.Produces`, `.WithName` and `.WithTags`
// extension methods on the `IEndpointRouteBuilder` used as hints to build the swagger UI 
// Todo: opt-in only
if (builder.IsDevMode())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Path base cdp-portal-backend
app.UsePathBase("/cdp-portal-backend");
app.UseRouting();

// enable auth
app.UseAuthentication();
app.UseAuthorization();

// Add endpoints
app.MapDeployablesEndpoint(new SerilogLoggerFactory(logger)
    .CreateLogger(typeof(ArtifactsEndpoint)));
app.MapDeploymentsEndpoint();
app.MapLibrariesEndpoint();
app.MapRepositoriesEndpoint();
app.MapAdminEndpoint();
app.MapHealthChecks("/health");

// Start the ecs and ecr services
#pragma warning disable CS4014
var ecsSqsEventListener = app.Services.GetService<EcsEventListener>();
logger.Information("Starting ECS listener - reading service events from SQS");
Task.Run(() =>
    ecsSqsEventListener?.ReadAsync(app.Lifetime
        .ApplicationStopping)); // do not await this, we want it to run in the background

var ecrSqsEventListener = app.Services.GetService<EcrEventListener>();
logger.Information("Starting ECR listener - reading image creation events from SQS");
Task.Run(() =>
    ecrSqsEventListener?.ReadAsync(app.Lifetime
        .ApplicationStopping)); // do not await this, we want it to run in the background
#pragma warning restore CS4014

app.Run();