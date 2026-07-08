using ArturRios.Data.Export.Attributes;

namespace ArturRios.Data.Tests.Export.TestSupport;

/// <summary>Simple record with value equality, for round-trip content assertions.</summary>
public record Widget
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

/// <summary>Drives column-mapping behavior (ignore, rename, reorder).</summary>
public class AttributedRow
{
    [ExportColumn(Order = 0, Name = "Identifier")] public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    [ExportIgnore] public string Internal { get; set; } = string.Empty;
}
