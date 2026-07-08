using ArturRios.Data.Export.Configuration;
using ArturRios.Output;

namespace ArturRios.Data.Export.Exporters;

/// <summary>Writes one line per record: default <see cref="object.ToString" />, or a custom selector.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">TXT options.</param>
public class TxtExporter<T>(TxtOptions options) : ExporterBase<T> where T : class
{
    /// <inheritdoc />
    protected override Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct) =>
        WriteLinesAsync(data, destination, item => item?.ToString() ?? string.Empty, ct);

    /// <summary>Writes each record on its own line using <paramref name="lineSelector" />.</summary>
    public Task<ProcessOutput> WriteAsync(IEnumerable<T> data, Stream destination, Func<T, string> lineSelector,
        CancellationToken ct = default) =>
        GuardedWriteAsync(data, destination, stream => WriteLinesAsync(data, stream, lineSelector, ct));

    /// <summary>Writes each record on its own line to a file using <paramref name="lineSelector" />.</summary>
    public Task<ProcessOutput> WriteToFileAsync(IEnumerable<T> data, string path, Func<T, string> lineSelector,
        CancellationToken ct = default) =>
        GuardedFileAsync(data, path, stream => WriteLinesAsync(data, stream, lineSelector, ct));

    private async Task WriteLinesAsync(IEnumerable<T> data, Stream destination, Func<T, string> lineSelector,
        CancellationToken ct)
    {
        await using var writer = new StreamWriter(destination, options.Encoding, leaveOpen: true);

        foreach (var item in data)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteAsync(lineSelector(item));
            await writer.WriteAsync(options.NewLine);
        }

        await writer.FlushAsync(ct);
    }
}
