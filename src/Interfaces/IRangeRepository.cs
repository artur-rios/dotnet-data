namespace ArturRios.Data.Interfaces;

/// <summary>
/// Batch mutation contract for entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface IRangeRepository<T> where T : Entity
{
    /// <summary>
    /// Applies changes to a collection of existing entities.
    /// </summary>
    /// <returns>The updated entities.</returns>
    IEnumerable<T> UpdateRange(List<T> entities);

    /// <summary>
    /// Removes entities by their identifiers.
    /// </summary>
    /// <returns>The identifiers of the deleted entities.</returns>
    IEnumerable<int> DeleteRange(List<int> ids);
}
