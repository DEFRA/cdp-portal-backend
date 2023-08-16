using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;

public class Layer
{
    public Layer(string digest, List<LayerFile> files)
    {
        Digest = digest;
        Files = files;
    }

    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; }

    public string Digest { get; init; }

    public List<LayerFile> Files { get; init; }
}

public record LayerFile(string FileName, string Content);