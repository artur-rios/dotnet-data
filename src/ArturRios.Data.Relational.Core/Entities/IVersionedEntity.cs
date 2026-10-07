namespace ArturRios.Data.Relational.Core.Entities;

/// <summary>
///     Contract for entities that participate in optimistic concurrency checks.
///     The context regenerates <see cref="ConcurrencyStamp" /> whenever such an entity is updated.
/// </summary>
public interface IVersionedEntity
{
    /// <summary>
    ///     Optimistic concurrency token. Regenerated whenever the entity is updated.
    /// </summary>
    Guid ConcurrencyStamp { get; set; }
}
