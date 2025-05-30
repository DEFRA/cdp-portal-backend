using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using static Defra.Cdp.Backend.Api.Services.Aws.Deployments.DeploymentStatus;

namespace Defra.Cdp.Backend.Api.Models;

public class Undeployment
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;

    public string CdpUndeploymentId { get; init; } = default!;

    public string Environment { get; init; } = default!;
    public string Service { get; init; } = default!;

    public UserDetails? User { get; init; }

    public DateTime Created { get; init; }
    public DateTime Updated { get; set; }

    public string Status { get; set; } = "";

    public static Undeployment FromRequest(RequestedUndeployment req)
    {
        return new Undeployment
        {
            CdpUndeploymentId = req.UndeploymentId,
            Environment = req.Environment,
            Service = req.Service,
            User = req.User,
            Created = DateTime.Now,
            Updated = DateTime.Now,
            Status = Undeployed
        };
    }

}