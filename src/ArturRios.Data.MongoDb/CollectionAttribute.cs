namespace ArturRios.Data.MongoDb;

/// <summary>Overrides the MongoDB collection name for a document type.</summary>
/// <param name="name">The collection name to use.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CollectionAttribute(string name) : Attribute
{
    /// <summary>The collection name.</summary>
    public string Name { get; } = name;
}
