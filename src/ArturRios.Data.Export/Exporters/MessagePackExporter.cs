using ArturRios.Data.Export.Configuration;
using MessagePack;

namespace ArturRios.Data.Export.Exporters;

/// <summary>Serializes the collection to MessagePack (the binary format) using the contractless resolver.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">MessagePack options.</param>
public class MessagePackExporter<T>(MessagePackOptions options) : ExporterBase<T> where T : class
{
    /// <inheritdoc />
    protected override Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct)
    {
        var array = data as T[] ?? data.ToArray();
        return MessagePackSerializer.SerializeAsync(destination, array, options.Effective, ct);
    }
}
