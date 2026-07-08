using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.Configuration;

namespace ArturRios.Data.Export.Exporters;

/// <summary>Writes records as RFC 4180 CSV using the shared column map.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">CSV options.</param>
public class CsvExporter<T>(CsvOptions options) : ExporterBase<T> where T : class
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
