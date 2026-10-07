using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Output;

namespace ArturRios.Data.Relational.Core.Interfaces;

/// <summary>
///     Full read/write repository contract for entities of type <typeparamref name="T" /> keyed by <typeparamref name="TKey" />.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity{TKey}" />.</typeparam>
/// <typeparam name="TKey">The entity's primary key type.</typeparam>
public interface IRepository<T, TKey> : IReadOnlyRepository<T, TKey>
    where T : Entity<TKey> where TKey : IEquatable<TKey>
{
    /// <summary>Persists a new entity and returns its generated identifier.</summary>
    DataOutput<TKey> Create(T entity);

    /// <summary>Persists multiple new entities and returns their generated identifiers.</summary>
    DataOutput<IEnumerable<TKey>> CreateRange(IEnumerable<T> entities);

    /// <summary>Applies changes to an existing entity.</summary>
    DataOutput<T> Update(T entity);

    /// <summary>Applies changes to multiple existing entities.</summary>
    DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> entities);

    /// <summary>Removes an entity and returns its identifier.</summary>
    DataOutput<TKey> Delete(T entity);

    /// <summary>Removes entities by identifier and returns the deleted identifiers.</summary>
    DataOutput<IEnumerable<TKey>> DeleteRange(IEnumerable<TKey> ids);
}
