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
///     Text that a spreadsheet would evaluate as a formula is prefixed with <c>'</c> unless
///     <see cref="CsvOptions.EscapeFormulas" /> is off.
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
        var writer = new StreamWriter(destination, options.Encoding, leaveOpen: true);

        await using var writerScope = writer.ConfigureAwait(false);

        if (options.IncludeHeader)
        {
            await writer.WriteLineAsync(string.Join(options.Delimiter,
                columns.Select(c => Escape(c.Header, options.Delimiter)))).ConfigureAwait(false);
        }

        foreach (var item in data)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(options.Delimiter,
                columns.Select(c => Escape(Render(c.Getter(item)), options.Delimiter)))).ConfigureAwait(false);
        }

        await writer.FlushAsync(ct).ConfigureAwait(false);
    }

    // Characters that make a spreadsheet treat a cell as a formula (OWASP "CSV Injection").
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    private string Render(object? value)
    {
        var text = ValueRenderer.Render(value);

        // Only text is neutralized: a formatted value (number, date, enum, Guid) is produced by the runtime,
        // not by whoever supplied the data, and a negative number must stay a number.
        var isText = value is string or char || value is not null and not IFormattable;

        return options.EscapeFormulas && isText && text.Length > 0 && FormulaTriggers.Contains(text[0])
            ? "'" + text
            : text;
    }

    private static string Escape(string field, char delimiter)
    {
        var mustQuote = field.Contains(delimiter) || field.Contains('"') || field.Contains('\n') || field.Contains('\r');
        return mustQuote ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
    }
}
