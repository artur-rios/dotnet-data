using System.ComponentModel.DataAnnotations.Schema;

namespace ArturRios.Data;

public abstract class Entity
{
    /// <summary>
    /// The unique identifier for the entity.
    /// </summary>
    [Column(Order = 1)] public int Id { get; set; }
}
