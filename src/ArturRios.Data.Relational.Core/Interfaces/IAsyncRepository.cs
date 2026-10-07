using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Output;

namespace ArturRios.Data.Relational.Core.Interfaces;

/// <summary>
///     Full asynchronous read/write repository contract for entities of type <typeparamref name="T" />
///     keyed by <typeparamref name="TKey" />.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity{TKey}" />.</typeparam>
/// <typeparam name="TKey">The entity's primary key type.</typeparam>
public interface IAsyncRepository<T, TKey> : IAsyncReadOnlyRepository<T, TKey>
    where T : Entity<TKey> where TKey : IEquatable<TKey>
{
    /// <summary>Persists a new entity and returns its generated identifier.</summary>
    Task<DataOutput<TKey>> CreateAsync(T entity, CancellationToken ct = default);

    /// <summary>Persists multiple new entities and returns their generated identifiers.</summary>
    Task<DataOutput<IEnumerable<TKey>>> CreateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    /// <summary>Applies changes to an existing entity.</summary>
    Task<DataOutput<T>> UpdateAsync(T entity, CancellationToken ct = default);

    /// <summary>Applies changes to multiple existing entities.</summary>
    Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    /// <summary>Removes an entity and returns its identifier.</summary>
    Task<DataOutput<TKey>> DeleteAsync(T entity, CancellationToken ct = default);

    /// <summary>Removes entities by identifier and returns the deleted identifiers.</summary>
    Task<DataOutput<IEnumerable<TKey>>> DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken ct = default);
}
