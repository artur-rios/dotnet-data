using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Export.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Export.Abstractions;

/// <summary>Resolves exporters from the DI container by <see cref="ExportFormat" />.</summary>
/// <param name="serviceProvider">The service provider holding the registered exporters.</param>
public class ExporterFactory(IServiceProvider serviceProvider) : IExporterFactory
{
    /// <inheritdoc />
    public IExporter<T> Resolve<T>(ExportFormat format) where T : class
    {
        var openType = format switch
        {
            ExportFormat.Csv => typeof(CsvExporter<>),
            ExportFormat.Json => typeof(JsonExporter<>),
            ExportFormat.Txt => typeof(TxtExporter<>),
            ExportFormat.MessagePack => typeof(MessagePackExporter<>),
            ExportFormat.Excel => ResolveExcelType(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        return (IExporter<T>)serviceProvider.GetRequiredService(openType.MakeGenericType(typeof(T)));
    }

    private Type ResolveExcelType()
    {
        var registration = serviceProvider.GetService<ExcelExporterRegistration>()
            ?? throw new NotSupportedException(
                "Excel export requires the ArturRios.Data.Export.Excel package — call AddExcelExport().");

        return registration.ExporterType;
    }
}
