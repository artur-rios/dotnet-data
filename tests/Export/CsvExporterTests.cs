using System.Text;
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;

namespace ArturRios.Data.Tests.Export;

[Trait("Category", "Unit")]
public class CsvExporterTests
{
    private static async Task<string> WriteAsync<T>(CsvExporter<T> exporter, IEnumerable<T> data) where T : class
    {
        using var stream = new MemoryStream();
        var result = await exporter.WriteAsync(data, stream);
        Assert.True(result.Success);
        return new UTF8Encoding(false).GetString(stream.ToArray());
    }

    [Fact]
    public async Task GivenRecords_WhenWritingCsv_ThenTheHeaderAndEveryRowAreWritten()
    {
        var text = await WriteAsync(new CsvExporter<Widget>(new CsvOptions()),
            [new Widget { Id = 1, Name = "a", Price = 2.5m }]);

        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Id,Name,Price", lines[0]);
        Assert.Equal("1,a,2.5", lines[1]);
    }

    [Fact]
    public async Task GivenFieldsHoldingDelimitersOrQuotes_WhenWritingCsv_ThenTheyAreQuotedAndEscaped()
    {
        var text = await WriteAsync(new CsvExporter<Widget>(new CsvOptions()),
            [new Widget { Id = 1, Name = "Hello, \"World\"", Price = 0m }]);

        Assert.Contains("\"Hello, \"\"World\"\"\"", text);
    }

    [Fact]
    public async Task GivenNoRecords_WhenWritingCsv_ThenOnlyTheHeaderIsWritten()
    {
        var text = await WriteAsync(new CsvExporter<Widget>(new CsvOptions()), []);
        Assert.Equal("Id,Name,Price", text.Trim());
    }

    [Fact]
    public async Task GivenTheHeaderIsDisabled_WhenWritingCsv_ThenNoHeaderIsWritten()
    {
        var text = await WriteAsync(new CsvExporter<Widget>(new CsvOptions { IncludeHeader = false }),
            [new Widget { Id = 1, Name = "a", Price = 1m }]);
        Assert.StartsWith("1,a,1", text);
    }

    [Fact]
    public async Task GivenACustomDelimiter_WhenWritingCsv_ThenItIsUsed()
    {
        var text = await WriteAsync(new CsvExporter<Widget>(new CsvOptions { Delimiter = ';' }),
            [new Widget { Id = 1, Name = "a", Price = 1m }]);
        Assert.Contains("Id;Name;Price", text);
    }

    [Fact]
    public async Task GivenAFieldContainingANewline_WhenWritingCsv_ThenItIsQuoted()
    {
        var text = await WriteAsync(new CsvExporter<Widget>(new CsvOptions()),
            [new Widget { Id = 1, Name = "line1\nline2\rline3", Price = 0m }]);

        Assert.Contains("\"line1\nline2\rline3\"", text);
    }

    private sealed class Signed
    {
        public string Note { get; set; } = string.Empty;
        public int Delta { get; set; }
        public decimal Amount { get; set; }
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+SUM(A1:A2)", "'+SUM(A1:A2)")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tcmd", "'\tcmd")]
    [InlineData("plain", "plain")]
    [InlineData("a=b", "a=b")]
    public async Task GivenTextThatASpreadsheetWouldEvaluate_WhenWritingCsv_ThenItIsPrefixedWithAQuote(
        string note, string expected)
    {
        var text = await WriteAsync(new CsvExporter<Signed>(new CsvOptions { IncludeHeader = false }),
            [new Signed { Note = note, Delta = 1, Amount = 1m }]);

        Assert.Equal($"{expected},1,1{Environment.NewLine}", text);
    }

    [Fact]
    public async Task GivenAFormulaThatNeedsQuoting_WhenWritingCsv_ThenItIsPrefixedAndQuoted()
    {
        var text = await WriteAsync(new CsvExporter<Signed>(new CsvOptions { IncludeHeader = false }),
            [new Signed { Note = "=HYPERLINK(\"http://x\",\"y\")", Delta = 1, Amount = 1m }]);

        Assert.StartsWith("\"'=HYPERLINK(\"\"http://x\"\",\"\"y\"\")\",", text);
    }

    [Fact]
    public async Task GivenNegativeNumbers_WhenWritingCsv_ThenTheyAreWrittenAsNumbers()
    {
        var text = await WriteAsync(new CsvExporter<Signed>(new CsvOptions { IncludeHeader = false }),
            [new Signed { Note = "n", Delta = -5, Amount = -2.5m }]);

        Assert.Equal($"n,-5,-2.5{Environment.NewLine}", text);
    }

    [Fact]
    public async Task GivenFormulaEscapingIsOff_WhenWritingCsv_ThenTextIsWrittenVerbatim()
    {
        var text = await WriteAsync(
            new CsvExporter<Signed>(new CsvOptions { IncludeHeader = false, EscapeFormulas = false }),
            [new Signed { Note = "=1+1", Delta = 1, Amount = 1m }]);

        Assert.Equal($"=1+1,1,1{Environment.NewLine}", text);
    }
}
