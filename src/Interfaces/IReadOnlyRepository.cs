using ArturRios.Output;

namespace ArturRios.Data.Core.Interfaces;

/// <summary>
/// Read-only repository contract for entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface IReadOnlyRepository<T> where T : Entity
{
    /// <summary>
    /// Returns a deferred, composable query over the entity set. Performs no I/O until materialized.
    /// </summary>
    IQueryable<T> Query();

    /// <summary>
    /// Returns all entities, enveloped in a <see cref="DataOutput{T}"/>.
    /// </summary>
    DataOutput<IEnumerable<T>> GetAll();

    /// <summary>
    /// Returns the entity with the given identifier, or a successful result with
    /// <c>null</c> data when none matches.
    /// </summary>
    DataOutput<T?> GetById(int id);
}
