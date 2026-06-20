namespace ArturRios.Data.Interfaces;

/// <summary>
/// Full CRUD repository contract for entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface ICrudRepository<T> where T : Entity
{
    /// <summary>
    /// Persists a new entity.
    /// </summary>
    /// <returns>The identifier of the created entity.</returns>
    int Create(T entity);

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

    /// <summary>
    /// Applies changes to an existing entity.
    /// </summary>
    /// <returns>The updated entity.</returns>
    T Update(T entity);

    /// <summary>
    /// Removes an entity.
    /// </summary>
    /// <returns>The identifier of the deleted entity.</returns>
    int Delete(T entity);
}
