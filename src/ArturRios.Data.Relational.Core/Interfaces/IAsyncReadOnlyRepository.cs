using ArturRios.Output;

namespace ArturRios.Data.Relational.Core.Interfaces;

/// <summary>
///     Asynchronous read-only repository contract for entities of type <typeparamref name="T" />.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity" />.</typeparam>
public interface IAsyncReadOnlyRepository<T> where T : Entity
{
    /// <summary>
    ///     Returns a deferred, composable query over the entity set. Performs no I/O until materialized.
    /// </summary>
    IQueryable<T> Query();

    /// <summary>Returns all entities, enveloped in a <see cref="DataOutput{T}" />.</summary>
    Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    ///     Returns the entity with the given identifier, or a successful result with
    ///     <c>null</c> data when none matches.
    /// </summary>
    Task<DataOutput<T?>> GetByIdAsync(int id, CancellationToken ct = default);
}
