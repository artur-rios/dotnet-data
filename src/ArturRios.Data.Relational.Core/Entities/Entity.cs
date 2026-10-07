using System.ComponentModel.DataAnnotations.Schema;

namespace ArturRios.Data.Relational.Core.Entities;

/// <summary>
///     Abstract base class for data entities keyed by an identifier of type <typeparamref name="TKey" />.
/// </summary>
/// <typeparam name="TKey">The primary key type, e.g. <see cref="long" />, <see cref="Guid" /> or <see cref="string" />.</typeparam>
public abstract class Entity<TKey> where TKey : IEquatable<TKey>
{
    /// <summary>
    ///     The unique identifier for the entity.
    /// </summary>
    [Column(Order = 1)]
    public TKey Id { get; set; } = default!;
}
