using System.ComponentModel.DataAnnotations;

namespace ArturRios.Data.Core;

/// <summary>
/// Base class for entities that participate in optimistic concurrency checks.
/// The <see cref="ConcurrencyStamp"/> is regenerated on every update by the context,
/// so a stale value causes the update to fail with a concurrency conflict.
/// </summary>
public abstract class VersionedEntity : Entity
{
    /// <summary>
    /// Optimistic concurrency token. Regenerated whenever the entity is updated.
    /// </summary>
    [ConcurrencyCheck]
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
