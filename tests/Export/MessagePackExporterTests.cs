using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;
using MessagePack;

namespace ArturRios.Data.Tests.Export;

public class MessagePackExporterTests
{
    [Fact]
    public async Task WriteAsync_RoundTripsCollection()
    {
        var input = new[] { new Widget { Id = 1, Name = "a", Price = 2.5m }, new Widget { Id = 2, Name = "b", Price = 3m } };
        var options = new MessagePackOptions();
        using var stream = new MemoryStream();

        var result = await new MessagePackExporter<Widget>(options).WriteAsync(input, stream);
        Assert.True(result.Success);

        stream.Position = 0;
        var output = await MessagePackSerializer.DeserializeAsync<Widget[]>(stream, options.Effective);
        Assert.Equal(input, output);
    }

    [Fact]
    public async Task WriteAsync_EmptyCollection_RoundTripsEmpty()
    {
        var options = new MessagePackOptions();
        using var stream = new MemoryStream();
        await new MessagePackExporter<Widget>(options).WriteAsync([], stream);

        stream.Position = 0;
        var output = await MessagePackSerializer.DeserializeAsync<Widget[]>(stream, options.Effective);
        Assert.Empty(output);
    }
}
