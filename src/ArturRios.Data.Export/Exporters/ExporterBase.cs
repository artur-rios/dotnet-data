using ArturRios.Data.Export.Interfaces;
using ArturRios.Output;
using Microsoft.Extensions.Logging;

namespace ArturRios.Data.Export.Exporters;

/// <summary>
///     Base for exporters: handles null-guarding, envelope conversion, cancellation propagation, and
///     file-stream lifetime. Concrete exporters implement <see cref="WriteCoreAsync" />.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="logger">
///     Optional logger. Envelopes carry no exception text, so a write failure is otherwise
///     undiagnosable: supply a logger and the full exception, plus the destination path for file
///     writes, is written at <see cref="LogLevel.Error" />. Record contents are never logged.
///     Resolved from DI when logging is registered.
/// </param>
public abstract class ExporterBase<T>(ILogger? logger = null) : IExporter<T> where T : class
{
    /// <summary>Message returned when a write fails.</summary>
    protected const string ExportFailedMessage = "An export error occurred.";

    /// <summary>Message returned when the caller passes no records.</summary>
    protected const string NullDataMessage = "An export error occurred: data is null.";

    /// <summary>Message returned when the caller passes no destination stream.</summary>
    protected const string NullDestinationMessage = "An export error occurred: destination is null.";

    /// <summary>Message returned when the caller passes no destination path.</summary>
    protected const string EmptyPathMessage = "An export error occurred: path is null or empty.";

    /// <inheritdoc />
    public Task<ProcessOutput> WriteAsync(IEnumerable<T> data, Stream destination, CancellationToken ct = default) =>
        GuardedWriteAsync(data, destination, stream => WriteCoreAsync(data, stream, ct));

    /// <inheritdoc />
    public Task<ProcessOutput> WriteToFileAsync(IEnumerable<T> data, string path, CancellationToken ct = default) =>
        GuardedFileAsync(data, path, stream => WriteCoreAsync(data, stream, ct));

    /// <summary>Guards a stream write: null checks, envelope conversion, cancellation propagation.</summary>
    protected async Task<ProcessOutput> GuardedWriteAsync(IEnumerable<T> data, Stream destination,
        Func<Stream, Task> write)
    {
        if (data is null) return ProcessOutput.New.WithError(NullDataMessage);
        if (destination is null) return ProcessOutput.New.WithError(NullDestinationMessage);

        try
        {
            await write(destination);
            return ProcessOutput.New;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Fail(ex, destination: null); }
    }

    /// <summary>Guards a file write: opens/truncates the file, then delegates to <paramref name="write" />.</summary>
    protected async Task<ProcessOutput> GuardedFileAsync(IEnumerable<T> data, string path, Func<Stream, Task> write)
    {
        if (data is null) return ProcessOutput.New.WithError(NullDataMessage);
        if (string.IsNullOrEmpty(path)) return ProcessOutput.New.WithError(EmptyPathMessage);

        try
        {
            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await write(stream);
            return ProcessOutput.New;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Fail(ex, path); }
    }

    /// <summary>
    ///     Logs the failure when a logger is configured, and returns the caller-safe envelope.
    ///     Exception text embeds absolute paths and OS error detail, so it goes to the log and
    ///     never to the caller.
    /// </summary>
    /// <param name="ex">The exception caught by a guard.</param>
    /// <param name="destination">The file path being written, or <see langword="null" /> for a stream write.</param>
    protected ProcessOutput Fail(Exception ex, string? destination)
    {
        logger?.LogError(ex, "Export failed. Exporter: {Exporter}, record: {Record}, destination: {Destination}",
            GetType().Name, typeof(T).Name, destination ?? "<stream>");

        return ProcessOutput.New.WithError(ExportFailedMessage);
    }

    /// <summary>Format-specific write. Implementations must honor <paramref name="ct" /> and not dispose the stream.</summary>
    protected abstract Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct);
}
