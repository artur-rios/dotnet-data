using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.Configuration;
using Microsoft.Extensions.Logging;

namespace ArturRios.Data.Export.Exporters;

/// <summary>Writes records as delimited text using the shared column map and RFC 4180 quoting.</summary>
/// <remarks>
///     Quoting follows RFC 4180: a field is quoted when it holds the delimiter, a double quote or a line
///     break, and an embedded quote is doubled. The delimiter and the line terminator are not fixed to the
///     RFC's comma and CRLF - the delimiter comes from the options and lines end with the platform's
///     newline - so the output is RFC 4180 only when those happen to match.
/// </remarks>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">CSV options.</param>
/// <param name="logger">Optional logger; see <see cref="ExporterBase{T}" />.</param>
public class CsvExporter<T>(CsvOptions options, ILogger<CsvExporter<T>>? logger = null)
    : ExporterBase<T>(logger) where T : class
{
    /// <inheritdoc />
    protected override async Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct)
    {
        var columns = ColumnMap.For<T>();
        await using var writer = new StreamWriter(destination, options.Encoding, leaveOpen: true);

        if (options.IncludeHeader)
        {
            await writer.WriteLineAsync(string.Join(options.Delimiter,
                columns.Select(c => Escape(c.Header, options.Delimiter))));
        }

        foreach (var item in data)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(options.Delimiter,
                columns.Select(c => Escape(ValueRenderer.Render(c.Getter(item)), options.Delimiter))));
        }

        await writer.FlushAsync(ct);
    }

    private static string Escape(string field, char delimiter)
    {
        var mustQuote = field.Contains(delimiter) || field.Contains('"') || field.Contains('\n') || field.Contains('\r');
        return mustQuote ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
    }
}
