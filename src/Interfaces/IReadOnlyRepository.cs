namespace ArturRios.Data.Interfaces;

/// <summary>
/// Read-only repository contract for querying entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface IReadOnlyRepository<out T> where T : Entity
{
    /// <summary>
    /// Returns all entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <returns>An <see cref="IQueryable{T}"/> of all entities.</returns>
    IQueryable<T> GetAll();

    /// <summary>
    /// Returns the entity with the specified identifier.
    /// </summary>
    /// <returns>The matching entity, or <see langword="null"/> if not found.</returns>
    T? GetById(int id);
}
