using System.Text;
using System.Text.Json;
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;

namespace ArturRios.Data.Tests.Export;

public class JsonExporterTests
{
    [Fact]
    public async Task WriteAsync_RoundTripsCollection()
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
    public async Task WriteAsync_EmptyCollection_WritesEmptyArray()
    {
        using var stream = new MemoryStream();
        await new JsonExporter<Widget>(new JsonOptions()).WriteAsync([], stream);
        Assert.Equal("[]", Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public async Task WriteAsync_WriteIndented_ProducesIndentedJson()
    {
        using var stream = new MemoryStream();
        await new JsonExporter<Widget>(new JsonOptions { WriteIndented = true })
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 1m }], stream);

        Assert.Contains("\n", Encoding.UTF8.GetString(stream.ToArray()));
    }
}
