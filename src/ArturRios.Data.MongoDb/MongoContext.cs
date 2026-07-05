using MongoDB.Driver;

namespace ArturRios.Data.MongoDb;

/// <summary>
/// Wraps an <see cref="IMongoDatabase"/> and carries the ambient client session used to enlist
/// repository operations in the Mongo unit of work transaction.
/// </summary>
/// <param name="database">The MongoDB database.</param>
public class MongoContext(IMongoDatabase database)
{
    /// <summary>The ambient transaction session, or <see langword="null"/> when none is active.</summary>
    public IClientSessionHandle? Session { get; set; }

    /// <summary>Returns the collection for <typeparamref name="T"/> using the naming convention.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    public IMongoCollection<T> GetCollection<T>() where T : Document =>
        database.GetCollection<T>(CollectionName.For<T>());
}
