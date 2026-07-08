using ArturRios.Data.Export.Exporters;

namespace ArturRios.Data.Tests.Export;

public class ExporterBaseTests
{
    private sealed class OkExporter : ExporterBase<string>
    {
        protected override async Task WriteCoreAsync(IEnumerable<string> data, Stream destination, CancellationToken ct)
        {
            await using var writer = new StreamWriter(destination, leaveOpen: true);
            foreach (var s in data) await writer.WriteAsync(s);
            await writer.FlushAsync(ct);
        }
    }

    private sealed class ThrowingExporter : ExporterBase<string>
    {
        protected override Task WriteCoreAsync(IEnumerable<string> data, Stream destination, CancellationToken ct)
            => throw new InvalidOperationException("boom");
    }

    private sealed class CancelExporter : ExporterBase<string>
    {
        protected override Task WriteCoreAsync(IEnumerable<string> data, Stream destination, CancellationToken ct)
            => throw new OperationCanceledException();
    }

    [Fact]
    public async Task WriteAsync_Success_ReturnsSuccessAndLeavesStreamOpen()
    {
        using var stream = new MemoryStream();
        var result = await new OkExporter().WriteAsync(["a", "b"], stream);

        Assert.True(result.Success);
        Assert.True(stream.CanWrite); // not disposed
    }

    [Fact]
    public async Task WriteAsync_NullData_ReturnsError()
    {
        using var stream = new MemoryStream();
        var result = await new OkExporter().WriteAsync(null!, stream);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task WriteAsync_NullDestination_ReturnsError()
    {
        var result = await new OkExporter().WriteAsync(["a"], null!);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task WriteAsync_WhenCoreThrows_ReturnsError()
    {
        using var stream = new MemoryStream();
        var result = await new ThrowingExporter().WriteAsync(["a"], stream);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task WriteAsync_WhenCanceled_Propagates()
    {
        using var stream = new MemoryStream();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new CancelExporter().WriteAsync(["a"], stream));
    }

    [Fact]
    public async Task WriteToFileAsync_WritesFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.txt");
        try
        {
            var result = await new OkExporter().WriteToFileAsync(["hello"], path);
            Assert.True(result.Success);
            Assert.Equal("hello", await File.ReadAllTextAsync(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
