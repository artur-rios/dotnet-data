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
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 2.5m }], stream, TestContext.Current.CancellationToken);
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
        var result = await new ExcelExporter<Widget>(new ExcelExportOptions()).WriteAsync([], stream, TestContext.Current.CancellationToken);
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
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 1m }], stream, TestContext.Current.CancellationToken);

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
    private sealed class MixedRow
    {
        public bool Active { get; set; }
        public DateTime When { get; set; }
        public float Score { get; set; }
        public object? Missing { get; set; }
        public Guid Key { get; set; }
    }

    [Fact]
    public async Task GivenBooleanDateTimeAndOtherValues_WhenWritingAWorkbook_ThenEachGetsItsNativeCellType()
    {
        var key = Guid.NewGuid();
        using var stream = new MemoryStream();
        var result = await new ExcelExporter<MixedRow>(new ExcelExportOptions()).WriteAsync(
            [new MixedRow { Active = true, When = new DateTime(2026, 11, 16, 8, 30, 0), Score = 1.5f, Key = key }],
            stream, TestContext.Current.CancellationToken);
        Assert.True(result.Success);

        using var workbook = new XLWorkbook(new MemoryStream(stream.ToArray()));
        var ws = workbook.Worksheet(1);
        Assert.Equal(XLDataType.Boolean, ws.Cell(2, 1).DataType);
        Assert.True(ws.Cell(2, 1).GetBoolean());
        Assert.Equal(XLDataType.DateTime, ws.Cell(2, 2).DataType);
        Assert.Equal(new DateTime(2026, 11, 16, 8, 30, 0), ws.Cell(2, 2).GetDateTime());
        Assert.Equal(1.5, ws.Cell(2, 3).GetDouble());
        Assert.True(ws.Cell(2, 4).IsEmpty());
        Assert.Equal(key.ToString(), ws.Cell(2, 5).GetString());
    }

    [Fact]
    public async Task GivenANonFiniteSinglePrecisionNumber_WhenWritingAWorkbook_ThenItIsWrittenAsText()
    {
        using var stream = new MemoryStream();
        var result = await new ExcelExporter<MixedRow>(new ExcelExportOptions())
            .WriteAsync([new MixedRow { Score = float.NaN }], stream, TestContext.Current.CancellationToken);
        Assert.True(result.Success);

        using var workbook = new XLWorkbook(new MemoryStream(stream.ToArray()));
        Assert.Equal(XLDataType.Text, workbook.Worksheet(1).Cell(2, 3).DataType);
        Assert.Equal("NaN", workbook.Worksheet(1).Cell(2, 3).GetString());
    }

    [Fact]
    public void GivenAConfigurationDelegate_WhenAddingTheExcelAddOn_ThenTheConfiguredOptionsAreRegistered()
    {
        var services = new ServiceCollection();
        services.AddExcelExport(o => o.SheetName = "Configured");
        using var provider = services.BuildServiceProvider();

        Assert.Equal("Configured", provider.GetRequiredService<ExcelExportOptions>().SheetName);
    }

    public static TheoryData<ExportFormat> Formats =>
        [ExportFormat.Csv, ExportFormat.Json, ExportFormat.Txt, ExportFormat.MessagePack, ExportFormat.Excel];

    [Theory]
    [MemberData(nameof(Formats))]
    public async Task GivenAStreamThatRefusesSynchronousWrites_WhenExporting_ThenTheExportSucceeds(ExportFormat format)
    {
        var services = new ServiceCollection();
        services.AddExport();
        services.AddExcelExport();
        var factory = services.BuildServiceProvider().GetRequiredService<IExporterFactory>();
        var destination = new AsyncOnlyStream();

        var result = await factory.Resolve<Widget>(format)
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 2.5m }], destination,
                TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(", ", result.Errors));
        Assert.True(destination.Written.Length > 0);
    }

    [Fact]
    public async Task GivenAStreamThatRefusesSynchronousWrites_WhenWritingAWorkbook_ThenItIsAWorkbook()
    {
        var destination = new AsyncOnlyStream();

        var result = await new ExcelExporter<Widget>(new ExcelExportOptions())
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 2.5m }], destination,
                TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(", ", result.Errors));
        destination.Written.Position = 0;
        using var workbook = new XLWorkbook(destination.Written);
        Assert.Equal("a", workbook.Worksheet(1).Cell(2, 2).GetString());
    }

    [Fact]
    public async Task GivenACanceledToken_WhenWritingAnEmptyWorkbook_ThenTheCancellationPropagates()
    {
        using var stream = new MemoryStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ExcelExporter<Widget>(new ExcelExportOptions()).WriteAsync([], stream, new CancellationToken(true)));
        Assert.Equal(0, stream.Length);
    }
}
