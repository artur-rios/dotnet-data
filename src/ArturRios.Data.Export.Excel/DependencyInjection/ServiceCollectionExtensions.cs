using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.Excel.Configuration;
using ArturRios.Data.Export.Excel.Exporters;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Export.Excel.DependencyInjection;

/// <summary>Dependency-injection registration for the Excel export add-on.</summary>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Registers the Excel exporter and makes the exporter factory resolve <see cref="ExportFormat.Excel" />.</summary>
        /// <param name="configure">Optional Excel options configuration.</param>
        public IServiceCollection AddExcelExport(Action<ExcelExportOptions>? configure = null)
        {
            var options = new ExcelExportOptions();
            configure?.Invoke(options);

            services.AddSingleton(options);
            services.AddSingleton(typeof(ExcelExporter<>));
            services.AddSingleton(new ExcelExporterRegistration(typeof(ExcelExporter<>)));

            return services;
        }
    }
}
