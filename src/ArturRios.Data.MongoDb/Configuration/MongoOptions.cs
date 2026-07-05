namespace ArturRios.Data.MongoDb.Configuration;

/// <summary>Connection options for the MongoDB document store.</summary>
public class MongoOptions
{
    /// <summary>The MongoDB connection string.</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>The database name.</summary>
    public string DatabaseName { get; init; } = string.Empty;
}
