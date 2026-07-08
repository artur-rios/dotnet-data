using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.DependencyInjection;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.Export;

public class AddExportTests
{
    [Fact]
    public void AddExport_RegistersFactoryAndExporters()
    {
        var services = new ServiceCollection();
        services.AddExport();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IExporterFactory>());
        Assert.NotNull(provider.GetRequiredService<CsvExporter<Widget>>());
        Assert.NotNull(provider.GetRequiredService<JsonExporter<Widget>>());
        Assert.NotNull(provider.GetRequiredService<TxtExporter<Widget>>());
        Assert.NotNull(provider.GetRequiredService<MessagePackExporter<Widget>>());
    }

    [Fact]
    public void AddExport_AppliesConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddExport(o => o.Csv.Delimiter = ';');
        using var provider = services.BuildServiceProvider();

        Assert.Equal(';', provider.GetRequiredService<CsvOptions>().Delimiter);
    }
}
