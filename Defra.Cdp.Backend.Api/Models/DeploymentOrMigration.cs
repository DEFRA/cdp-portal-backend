using Defra.Cdp.Backend.Api.Services.Migrations;

namespace Defra.Cdp.Backend.Api.Models;

public class DeploymentOrMigration
{
    public Deployment? Deployment { get; set; }
    public DatabaseMigration? Migration { get; set; }
    public bool IsFavourite { get; set; }
    public DateTime Created { get; set; }
}