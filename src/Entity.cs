using System.ComponentModel.DataAnnotations.Schema;

namespace ArturRios.Data.Core;

/// <summary>
/// Abstract base class for all data entities. Provides a primary key identifier.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// The unique identifier for the entity.
    /// </summary>
    [Column(Order = 1)] public int Id { get; set; }
}
