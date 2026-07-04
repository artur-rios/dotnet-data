using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ArturRios.Data.MongoDb;

/// <summary>Base class for MongoDB documents. Maps a string identifier to the BSON <c>_id</c>.</summary>
public abstract class Document
{
    /// <summary>The document identifier (Mongo <c>_id</c>, stored as an ObjectId).</summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
}
