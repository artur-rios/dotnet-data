using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.Extensions.Logging;

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

    private sealed class LoggingThrowingExporter(ILogger logger) : ExporterBase<string>(logger)
    {
        protected override Task WriteCoreAsync(IEnumerable<string> data, Stream destination, CancellationToken ct)
            => throw new IOException(@"Could not find a part of the path 'C:\srv\app\exports\out.csv'.");
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
        Assert.Equal(["An export error occurred."], result.Errors);
    }

    [Fact]
    public async Task WriteToFileAsync_WhenCoreThrows_DoesNotLeakPathOrOsError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.txt");
        try
        {
            var result = await new ThrowingExporter().WriteToFileAsync(["a"], path);

            Assert.Equal(["An export error occurred."], result.Errors);
            Assert.All(result.Errors, e => Assert.DoesNotContain(path, e));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task WriteAsync_WithLogger_LogsExceptionDetail_ButEnvelopeStaysGeneric()
    {
        using var stream = new MemoryStream();
        var logger = new ListLogger<ExporterBase<string>>();

        var result = await new LoggingThrowingExporter(logger).WriteAsync(["a"], stream);

        Assert.Equal(["An export error occurred."], result.Errors);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("LoggingThrowingExporter", entry.Message);
        Assert.Contains(@"C:\srv\app\exports\out.csv", entry.Exception!.Message);
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
