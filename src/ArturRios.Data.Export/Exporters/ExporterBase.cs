using ArturRios.Data.Export.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Export.Exporters;

/// <summary>
///     Base for exporters: handles null-guarding, envelope conversion, cancellation propagation, and
///     file-stream lifetime. Concrete exporters implement <see cref="WriteCoreAsync" />.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
public abstract class ExporterBase<T> : IExporter<T> where T : class
{
    /// <summary>Prefix for enveloped error messages.</summary>
    protected const string ExportFailedMessage = "An export error occurred:";

    /// <inheritdoc />
    public Task<ProcessOutput> WriteAsync(IEnumerable<T> data, Stream destination, CancellationToken ct = default) =>
        GuardedWriteAsync(data, destination, stream => WriteCoreAsync(data, stream, ct));

    /// <inheritdoc />
    public Task<ProcessOutput> WriteToFileAsync(IEnumerable<T> data, string path, CancellationToken ct = default) =>
        GuardedFileAsync(data, path, stream => WriteCoreAsync(data, stream, ct));

    /// <summary>Guards a stream write: null checks, envelope conversion, cancellation propagation.</summary>
    protected async Task<ProcessOutput> GuardedWriteAsync(IEnumerable<T> data, Stream destination, Func<Stream, Task> write)
    {
        if (data is null) return ProcessOutput.New.WithError($"{ExportFailedMessage} data is null.");
        if (destination is null) return ProcessOutput.New.WithError($"{ExportFailedMessage} destination is null.");

        try
        {
            await write(destination);
            return ProcessOutput.New;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return ProcessOutput.New.WithError($"{ExportFailedMessage} {ex.GetBaseException().Message}"); }
    }

    /// <summary>Guards a file write: opens/truncates the file, then delegates to <paramref name="write" />.</summary>
    protected async Task<ProcessOutput> GuardedFileAsync(IEnumerable<T> data, string path, Func<Stream, Task> write)
    {
        if (data is null) return ProcessOutput.New.WithError($"{ExportFailedMessage} data is null.");
        if (string.IsNullOrEmpty(path)) return ProcessOutput.New.WithError($"{ExportFailedMessage} path is null or empty.");

        try
        {
            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await write(stream);
            return ProcessOutput.New;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return ProcessOutput.New.WithError($"{ExportFailedMessage} {ex.GetBaseException().Message}"); }
    }

    /// <summary>Format-specific write. Implementations must honor <paramref name="ct" /> and not dispose the stream.</summary>
    protected abstract Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct);
}
