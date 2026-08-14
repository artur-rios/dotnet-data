using System.Text.Json;
using ArturRios.Data.Export.Configuration;
using Microsoft.Extensions.Logging;

namespace ArturRios.Data.Export.Exporters;

/// <summary>Serializes the collection as a JSON array via System.Text.Json.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">JSON options.</param>
/// <param name="logger">Optional logger; see <see cref="ExporterBase{T}" />.</param>
public class JsonExporter<T>(JsonOptions options, ILogger<JsonExporter<T>>? logger = null)
    : ExporterBase<T>(logger) where T : class
{
    /// <inheritdoc />
    protected override Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct)
    {
        var serializerOptions = options.SerializerOptions ?? new JsonSerializerOptions { WriteIndented = options.WriteIndented };
        return JsonSerializer.SerializeAsync(destination, data, serializerOptions, ct);
    }
}
