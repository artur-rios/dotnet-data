using ArturRios.Output;

namespace ArturRios.Data.Export.Interfaces;

/// <summary>Writes a collection of records to a destination in a specific format.</summary>
/// <typeparam name="T">The record type.</typeparam>
public interface IExporter<T> where T : class
{
    /// <summary>Writes <paramref name="data" /> to <paramref name="destination" />. The stream is not disposed.</summary>
    Task<ProcessOutput> WriteAsync(IEnumerable<T> data, Stream destination, CancellationToken ct = default);

    /// <summary>Writes <paramref name="data" /> to the file at <paramref name="path" /> (created/truncated).</summary>
    Task<ProcessOutput> WriteToFileAsync(IEnumerable<T> data, string path, CancellationToken ct = default);
}
