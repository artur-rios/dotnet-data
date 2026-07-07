namespace ArturRios.Data.Export.Attributes;

/// <summary>Overrides the column header and/or ordinal position for a property in columnar exports (CSV, Excel).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExportColumnAttribute : Attribute
{
    /// <summary>Header text. When null, the property name is used.</summary>
    public string? Name { get; init; }

    /// <summary>Ordinal position (ascending). Unset columns sort last, then by property name.</summary>
    public int Order { get; init; } = int.MaxValue;
}
