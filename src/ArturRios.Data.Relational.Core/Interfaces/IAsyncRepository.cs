using ArturRios.Output;

namespace ArturRios.Data.Relational.Core.Interfaces;

/// <summary>
/// Full asynchronous read/write repository contract for entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface IAsyncRepository<T> : IAsyncReadOnlyRepository<T> where T : Entity
{
    /// <summary>Persists a new entity and returns its generated identifier.</summary>
    Task<DataOutput<int>> CreateAsync(T entity, CancellationToken ct = default);

    /// <summary>Persists multiple new entities and returns their generated identifiers.</summary>
    Task<DataOutput<IEnumerable<int>>> CreateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    /// <summary>Applies changes to an existing entity.</summary>
    Task<DataOutput<T>> UpdateAsync(T entity, CancellationToken ct = default);

    /// <summary>Applies changes to multiple existing entities.</summary>
    Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    /// <summary>Removes an entity and returns its identifier.</summary>
    Task<DataOutput<int>> DeleteAsync(T entity, CancellationToken ct = default);

    /// <summary>Removes entities by identifier and returns the deleted identifiers.</summary>
    Task<DataOutput<IEnumerable<int>>> DeleteRangeAsync(IEnumerable<int> ids, CancellationToken ct = default);
}
