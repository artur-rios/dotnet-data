using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.DependencyInjection;
using ArturRios.Data.Export.Excel.Configuration;
using ArturRios.Data.Export.Excel.DependencyInjection;
using ArturRios.Data.Export.Excel.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.Export;

public class ExcelExporterTests
{
    [Fact]
    public async Task WriteAsync_WritesHeaderAndRows()
    {
        using var stream = new MemoryStream();
        var result = await new ExcelExporter<Widget>(new ExcelExportOptions())
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 2.5m }], stream);
        Assert.True(result.Success);

        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet(1);
        Assert.Equal("Id", ws.Cell(1, 1).GetString());
        Assert.Equal("Name", ws.Cell(1, 2).GetString());
        Assert.Equal("a", ws.Cell(2, 2).GetString());
        Assert.Equal(2.5, ws.Cell(2, 3).GetDouble());
    }

    [Fact]
    public async Task WriteAsync_EmptyCollection_WritesHeaderOnly()
    {
        using var stream = new MemoryStream();
        var result = await new ExcelExporter<Widget>(new ExcelExportOptions()).WriteAsync([], stream);
        Assert.True(result.Success);

        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet(1);
        Assert.Equal("Id", ws.Cell(1, 1).GetString());
        Assert.True(ws.Cell(2, 1).IsEmpty());
    }

    [Fact]
    public async Task WriteAsync_UsesConfiguredSheetName()
    {
        using var stream = new MemoryStream();
        await new ExcelExporter<Widget>(new ExcelExportOptions { SheetName = "People" })
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 1m }], stream);

        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        Assert.Equal("People", workbook.Worksheet(1).Name);
    }

    [Fact]
    public void AddExcelExport_MakesFactoryResolveExcel()
    {
        var services = new ServiceCollection();
        services.AddExport();
        services.AddExcelExport();
        using var provider = services.BuildServiceProvider();

        var exporter = provider.GetRequiredService<IExporterFactory>().Resolve<Widget>(ExportFormat.Excel);
        Assert.IsType<ExcelExporter<Widget>>(exporter);
    }
}
