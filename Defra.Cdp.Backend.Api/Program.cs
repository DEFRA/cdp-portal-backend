using Amazon.ECR;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Services.Tenants;
using FluentValidation;
using Serilog;

//-------- Configure the WebApplication builder------------------//

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

// Swagger
builder.Services.AddSwaggerGen();

// Mongo
builder.Services.AddSingleton<IMongoDbClientFactory>(_ =>
    new MongoDbClientFactory(builder.Configuration.GetValue<string>("Mongo:DatabaseUri")));

// http client for deployables, to remove
builder.Services
    .AddHttpClient("deployables", client => { client.BaseAddress = new Uri("https://deployables.com/"); });

// SQS provider
builder.Services.AddSqsClient(builder.Configuration, true);

// Setup the services
builder.Services.Configure<EcsEventListenerOptions>(builder.Configuration.GetSection(EcsEventListenerOptions.Prefix));
builder.Services.Configure<DockerServiceOptions>(builder.Configuration.GetSection(DockerServiceOptions.Prefix));
builder.Services.Configure<DeployablesClientOptions>(builder.Configuration.GetSection(DeployablesClientOptions.Prefix));

if (builder.IsDevMode())
{
    builder.Services.AddSingleton<IDockerCredentialProvider, EmptyDockerCredentialProvider>();
}
else
{
    builder.Services.AddSingleton<IAmazonECR, AmazonECRClient>();
    builder.Services.AddSingleton<IDockerCredentialProvider, EcrCredentialProvider>();
}

builder.Services.AddSingleton<IDockerClient, DockerClient>();
builder.Services.AddSingleton<IArtifactScanner, ArtifactScanner>();
builder.Services.AddSingleton<IDeployablesService, DeployablesService>();
builder.Services.AddSingleton<IDeploymentsService, DeploymentsService>();
builder.Services.AddSingleton<IEventsService, EventsService>();
builder.Services.AddSingleton<ILayerService, LayerService>();
builder.Services.AddSingleton<EnvironmentLookup>();
builder.Services.AddSingleton<EcsEventListener>();

// Validators
// Add every validator we can find in the assembly that contains this Program
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//-------- Build and Setup the WebApplication------------------//
var app = builder.Build();

// Create swagger doc from internal endpoints then add the swagger ui endpoint
// Under `Endpoints` directory, the `.Produces`, `.WithName` and `.WithTags`
// extension methods on the `IEndpointRouteBuilder` used as hints to build the swagger UI 
app.UseSwagger();
app.UseSwaggerUI();

// Add endpoints
app.MapDeployablesEndpoint();
app.MapDeploymentsEndpoint();
app.MapAdminEndpoint();

app.Run();