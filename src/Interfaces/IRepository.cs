using ArturRios.Output;

namespace ArturRios.Data.Interfaces;

/// <summary>
/// Full read/write repository contract for entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface IRepository<T> : IReadOnlyRepository<T> where T : Entity
{
    /// <summary>Persists a new entity and returns its generated identifier.</summary>
    DataOutput<int> Create(T entity);

    /// <summary>Persists multiple new entities and returns their generated identifiers.</summary>
    DataOutput<IEnumerable<int>> CreateRange(IEnumerable<T> entities);

    /// <summary>Applies changes to an existing entity.</summary>
    DataOutput<T> Update(T entity);

    /// <summary>Applies changes to multiple existing entities.</summary>
    DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> entities);

    /// <summary>Removes an entity and returns its identifier.</summary>
    DataOutput<int> Delete(T entity);

    /// <summary>Removes entities by identifier and returns the deleted identifiers.</summary>
    DataOutput<IEnumerable<int>> DeleteRange(IEnumerable<int> ids);
}
