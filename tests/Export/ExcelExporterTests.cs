using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.DependencyInjection;
using ArturRios.Data.Export.Excel.Configuration;
using ArturRios.Data.Export.Excel.DependencyInjection;
using ArturRios.Data.Export.Excel.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.Export;

[Trait("Category", "Unit")]
public class ExcelExporterTests
{
    [Fact]
    public async Task GivenRecords_WhenWritingAWorkbook_ThenTheHeaderAndEveryRowAreWritten()
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
    public async Task GivenNoRecords_WhenWritingAWorkbook_ThenOnlyTheHeaderIsWritten()
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
    public async Task GivenAConfiguredSheetName_WhenWritingAWorkbook_ThenTheWorksheetCarriesIt()
    {
        using var stream = new MemoryStream();
        await new ExcelExporter<Widget>(new ExcelExportOptions { SheetName = "People" })
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 1m }], stream);

        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        Assert.Equal("People", workbook.Worksheet(1).Name);
    }

    [Fact]
    public void GivenTheExcelAddOn_WhenResolvingTheExcelFormat_ThenTheExcelExporterComesBack()
    {
        var services = new ServiceCollection();
        services.AddExport();
        services.AddExcelExport();
        using var provider = services.BuildServiceProvider();

        var exporter = provider.GetRequiredService<IExporterFactory>().Resolve<Widget>(ExportFormat.Excel);
        Assert.IsType<ExcelExporter<Widget>>(exporter);
    }

    private sealed class TypedRow
    {
        public double Ratio { get; set; }
        public DateOnly Day { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeOnly At { get; set; }
        public string Formula { get; set; } = string.Empty;
    }

    private static async Task<XLWorkbook> WriteSingleRowAsync(TypedRow row)
    {
        using var stream = new MemoryStream();
        var result = await new ExcelExporter<TypedRow>(new ExcelExportOptions()).WriteAsync([row], stream);
        Assert.True(result.Success);

        return new XLWorkbook(new MemoryStream(stream.ToArray()));
    }

    [Theory]
    [InlineData(double.NaN, "NaN")]
    [InlineData(double.PositiveInfinity, "Infinity")]
    [InlineData(double.NegativeInfinity, "-Infinity")]
    public async Task GivenANonFiniteNumber_WhenWritingAWorkbook_ThenTheExportSucceedsAndTheValueIsWrittenAsText(
        double value, string expected)
    {
        using var workbook = await WriteSingleRowAsync(new TypedRow { Ratio = value });
        var ws = workbook.Worksheet(1);

        Assert.Equal(XLDataType.Text, ws.Cell(2, 1).DataType);
        Assert.Equal(expected, ws.Cell(2, 1).GetString());
    }

    [Fact]
    public async Task GivenDateAndTimeValues_WhenWritingAWorkbook_ThenTheyAreNativeDateAndTimeCells()
    {
        using var workbook = await WriteSingleRowAsync(new TypedRow
        {
            Ratio = 1,
            Day = new DateOnly(2026, 10, 8),
            Duration = TimeSpan.FromMinutes(90),
            At = new TimeOnly(13, 30)
        });
        var ws = workbook.Worksheet(1);

        Assert.Equal(XLDataType.DateTime, ws.Cell(2, 2).DataType);
        Assert.Equal(new DateTime(2026, 10, 8), ws.Cell(2, 2).GetDateTime());
        Assert.Equal(XLDataType.TimeSpan, ws.Cell(2, 3).DataType);
        Assert.Equal(TimeSpan.FromMinutes(90), ws.Cell(2, 3).GetTimeSpan());
        Assert.Equal(XLDataType.TimeSpan, ws.Cell(2, 4).DataType);
        Assert.Equal(new TimeSpan(13, 30, 0), ws.Cell(2, 4).GetTimeSpan());
    }

    [Fact]
    public async Task GivenTextThatLooksLikeAFormula_WhenWritingAWorkbook_ThenItIsStoredAsTextNotAFormula()
    {
        using var workbook = await WriteSingleRowAsync(new TypedRow { Formula = "=HYPERLINK(\"http://x\")" });
        var ws = workbook.Worksheet(1);

        Assert.False(ws.Cell(2, 5).HasFormula);
        Assert.Equal(XLDataType.Text, ws.Cell(2, 5).DataType);
    }
}
