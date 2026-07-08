using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.Exporters;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Export.DependencyInjection;

/// <summary>Dependency-injection registration for the ArturRios.Data.Export core exporters.</summary>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Registers the exporter factory and the CSV/JSON/TXT/MessagePack exporters.</summary>
        /// <param name="configure">Optional options configuration.</param>
        public IServiceCollection AddExport(Action<ExportOptions>? configure = null)
        {
            var options = new ExportOptions();
            configure?.Invoke(options);

            services.AddSingleton(options.Csv);
            services.AddSingleton(options.Json);
            services.AddSingleton(options.Txt);
            services.AddSingleton(options.MessagePack);

            services.AddSingleton(typeof(CsvExporter<>));
            services.AddSingleton(typeof(JsonExporter<>));
            services.AddSingleton(typeof(TxtExporter<>));
            services.AddSingleton(typeof(MessagePackExporter<>));

            services.AddSingleton<IExporterFactory, ExporterFactory>();
            return services;
        }
    }
}
