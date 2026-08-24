using System.Text;
using System.Text.Json;
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;

namespace ArturRios.Data.Tests.Export;

[Trait("Category", "Unit")]
public class JsonExporterTests
{
    [Fact]
    public async Task GivenRecords_WhenWritingJson_ThenTheCollectionRoundTrips()
    {
        var input = new[] { new Widget { Id = 1, Name = "a", Price = 2.5m }, new Widget { Id = 2, Name = "b", Price = 3m } };
        using var stream = new MemoryStream();

        var result = await new JsonExporter<Widget>(new JsonOptions()).WriteAsync(input, stream);
        Assert.True(result.Success);

        stream.Position = 0;
        var output = await JsonSerializer.DeserializeAsync<List<Widget>>(stream);
        Assert.Equal(input, output);
    }

    [Fact]
    public async Task GivenNoRecords_WhenWritingJson_ThenAnEmptyArrayIsWritten()
    {
        using var stream = new MemoryStream();
        await new JsonExporter<Widget>(new JsonOptions()).WriteAsync([], stream);
        Assert.Equal("[]", Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public async Task GivenIndentationIsEnabled_WhenWritingJson_ThenTheOutputIsIndented()
    {
        using var stream = new MemoryStream();
        await new JsonExporter<Widget>(new JsonOptions { WriteIndented = true })
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 1m }], stream);

        Assert.Contains("\n", Encoding.UTF8.GetString(stream.ToArray()));
    }
}
