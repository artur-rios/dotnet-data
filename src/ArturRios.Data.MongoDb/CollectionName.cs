using System.Collections.Concurrent;
using System.Reflection;

namespace ArturRios.Data.MongoDb;

/// <summary>Resolves the MongoDB collection name for a document type.</summary>
public static class CollectionName
{
    private static readonly ConcurrentDictionary<Type, string> Cache = new();

    /// <summary>
    /// Returns the <see cref="MongoCollectionAttribute"/> name for <typeparamref name="T"/> if present,
    /// otherwise the type name.
    /// </summary>
    public static string For<T>() where T : Document =>
        Cache.GetOrAdd(typeof(T), static t => t.GetCustomAttribute<MongoCollectionAttribute>()?.Name ?? t.Name);
}
