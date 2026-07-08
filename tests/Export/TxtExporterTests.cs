using System.Text;
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;

namespace ArturRios.Data.Tests.Export;

public class TxtExporterTests
{
    private static string Read(MemoryStream stream) => new UTF8Encoding(false).GetString(stream.ToArray());

    [Fact]
    public async Task WriteAsync_DefaultsToToString()
    {
        using var stream = new MemoryStream();
        var options = new TxtOptions { NewLine = "\n" };
        var result = await new TxtExporter<Widget>(options)
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 1m }], stream);

        Assert.True(result.Success);
        Assert.Equal($"{new Widget { Id = 1, Name = "a", Price = 1m }}\n", Read(stream));
    }

    [Fact]
    public async Task WriteAsync_WithSelector_UsesSelector()
    {
        using var stream = new MemoryStream();
        var options = new TxtOptions { NewLine = "\n" };
        var result = await new TxtExporter<Widget>(options)
            .WriteAsync([new Widget { Name = "a" }, new Widget { Name = "b" }], stream, w => w.Name);

        Assert.True(result.Success);
        Assert.Equal("a\nb\n", Read(stream));
    }

    [Fact]
    public async Task WriteAsync_EmptyCollection_WritesNothing()
    {
        using var stream = new MemoryStream();
        await new TxtExporter<Widget>(new TxtOptions()).WriteAsync([], stream);
        Assert.Empty(stream.ToArray());
    }

    [Fact]
    public async Task WriteAsync_NullData_ReturnsError()
    {
        using var stream = new MemoryStream();
        var result = await new TxtExporter<Widget>(new TxtOptions()).WriteAsync(null!, stream, w => w.Name);
        Assert.False(result.Success);
    }
}
