using ArturRios.Data.Export.Interfaces;

namespace ArturRios.Data.Export.Abstractions;

/// <summary>Resolves the <see cref="IExporter{T}" /> for a given <see cref="ExportFormat" />.</summary>
public interface IExporterFactory
{
    /// <summary>Returns the exporter for <paramref name="format" />.</summary>
    /// <exception cref="NotSupportedException">The format's package is not registered (e.g. Excel add-on missing).</exception>
    IExporter<T> Resolve<T>(ExportFormat format) where T : class;
}
