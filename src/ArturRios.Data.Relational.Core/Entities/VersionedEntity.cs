using System.ComponentModel.DataAnnotations;

namespace ArturRios.Data.Relational.Core.Entities;

/// <summary>
///     Base class for entities keyed by <typeparamref name="TKey" /> that participate in optimistic
///     concurrency checks. The <see cref="ConcurrencyStamp" /> is regenerated on every update by the context,
///     so a stale value causes the update to fail with a concurrency conflict.
/// </summary>
/// <typeparam name="TKey">The primary key type.</typeparam>
public abstract class VersionedEntity<TKey> : Entity<TKey>, IVersionedEntity where TKey : IEquatable<TKey>
{
    /// <inheritdoc />
    [ConcurrencyCheck]
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
