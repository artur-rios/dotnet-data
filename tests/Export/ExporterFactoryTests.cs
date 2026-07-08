using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.DependencyInjection;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.Export;

public class ExporterFactoryTests
{
    private static IExporterFactory BuildFactory()
    {
        var services = new ServiceCollection();
        services.AddExport();
        return services.BuildServiceProvider().GetRequiredService<IExporterFactory>();
    }

    [Theory]
    [InlineData(ExportFormat.Csv, typeof(CsvExporter<Widget>))]
    [InlineData(ExportFormat.Json, typeof(JsonExporter<Widget>))]
    [InlineData(ExportFormat.Txt, typeof(TxtExporter<Widget>))]
    [InlineData(ExportFormat.MessagePack, typeof(MessagePackExporter<Widget>))]
    public void Resolve_ReturnsExpectedExporter(ExportFormat format, Type expected)
    {
        Assert.IsType(expected, BuildFactory().Resolve<Widget>(format));
    }

    [Fact]
    public void Resolve_Excel_WithoutAddOn_Throws()
    {
        var factory = BuildFactory();
        Assert.Throws<NotSupportedException>(() => factory.Resolve<Widget>(ExportFormat.Excel));
    }
}
