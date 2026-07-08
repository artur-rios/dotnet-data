namespace ArturRios.Data.Export.Attributes;

/// <summary>Excludes a property from columnar exports (CSV, Excel).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExportIgnoreAttribute : Attribute;
