namespace ArturRios.Data.Export.Abstractions;

/// <summary>
///     Marker registered by the Excel add-on so the core factory can resolve the Excel exporter without a
///     compile-time reference to it. Carries the open-generic exporter type.
/// </summary>
/// <param name="exporterType">The open-generic Excel exporter type, e.g. <c>typeof(ExcelExporter&lt;&gt;)</c>.</param>
public sealed class ExcelExporterRegistration(Type exporterType)
{
    /// <summary>The open-generic Excel exporter type.</summary>
    public Type ExporterType { get; } = exporterType;
}
