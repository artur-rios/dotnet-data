using ArturRios.Data.Export.Configuration;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace ArturRios.Data.Export.Exporters;

/// <summary>Serializes the collection to MessagePack (the binary format) using the contractless resolver.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">MessagePack options.</param>
/// <param name="logger">Optional logger; see <see cref="ExporterBase{T}" />.</param>
public class MessagePackExporter<T>(MessagePackOptions options, ILogger<MessagePackExporter<T>>? logger = null)
    : ExporterBase<T>(logger) where T : class
{
    /// <inheritdoc />
    protected override Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct)
    {
        var array = data as T[] ?? data.ToArray();
        return MessagePackSerializer.SerializeAsync(destination, array, options.Effective, ct);
    }
}
