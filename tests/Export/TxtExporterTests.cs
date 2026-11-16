using System.Text;
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;

namespace ArturRios.Data.Tests.Export;

[Trait("Category", "Unit")]
public class TxtExporterTests
{
    private static string Read(MemoryStream stream) => new UTF8Encoding(false).GetString(stream.ToArray());

    [Fact]
    public async Task GivenNoSelector_WhenWritingText_ThenEachRecordIsRenderedWithToString()
    {
        using var stream = new MemoryStream();
        var options = new TxtOptions { NewLine = "\n" };
        var result = await new TxtExporter<Widget>(options)
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 1m }], stream, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal($"{new Widget { Id = 1, Name = "a", Price = 1m }}\n", Read(stream));
    }

    [Fact]
    public async Task GivenALineSelector_WhenWritingText_ThenItIsUsedForEveryLine()
    {
        using var stream = new MemoryStream();
        var options = new TxtOptions { NewLine = "\n" };
        var result = await new TxtExporter<Widget>(options)
            .WriteAsync([new Widget { Name = "a" }, new Widget { Name = "b" }], stream, w => w.Name, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("a\nb\n", Read(stream));
    }

    [Fact]
    public async Task GivenNoRecords_WhenWritingText_ThenNothingIsWritten()
    {
        using var stream = new MemoryStream();
        await new TxtExporter<Widget>(new TxtOptions()).WriteAsync([], stream, TestContext.Current.CancellationToken);
        Assert.Empty(stream.ToArray());
    }

    [Fact]
    public async Task GivenNullData_WhenWritingText_ThenAnErrorEnvelopeComesBack()
    {
        using var stream = new MemoryStream();
        var result = await new TxtExporter<Widget>(new TxtOptions()).WriteAsync(null!, stream, w => w.Name, TestContext.Current.CancellationToken);
        Assert.False(result.Success);
    }
    [Fact]
    public async Task GivenALineSelector_WhenWritingTextToAFile_ThenTheFileHoldsOneSelectedLinePerRecord()
    {
        var directory = Directory.CreateTempSubdirectory("export-").FullName;
        var path = Path.Combine(directory, "out.txt");
        try
        {
            var result = await new TxtExporter<Widget>(new TxtOptions { NewLine = "\n" })
                .WriteToFileAsync([new Widget { Name = "a" }, new Widget { Name = "b" }], path, w => w.Name, TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal("a\nb\n", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            Assert.Equal([path], Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task GivenANullRecord_WhenWritingTextWithoutASelector_ThenItIsAnEmptyLine()
    {
        using var stream = new MemoryStream();

        var result = await new TxtExporter<Widget>(new TxtOptions { NewLine = "\n" })
            .WriteAsync([new Widget { Name = "a" }, null!], stream, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.EndsWith("\n\n", Read(stream));
    }
}
