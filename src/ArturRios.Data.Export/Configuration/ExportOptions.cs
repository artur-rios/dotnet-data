using System.Text;
using System.Text.Json;
using MessagePack;
using MessagePack.Resolvers;

namespace ArturRios.Data.Export.Configuration;

/// <summary>Options for the CSV exporter.</summary>
public class CsvOptions
{
    /// <summary>Field delimiter. Default ','.</summary>
    public char Delimiter { get; set; } = ',';

    /// <summary>Whether to write a header row from the column map. Default true.</summary>
    public bool IncludeHeader { get; set; } = true;

    /// <summary>Text encoding. Default UTF-8 without BOM.</summary>
    public Encoding Encoding { get; set; } = new UTF8Encoding(false);

    /// <summary>
    ///     Whether to neutralize text that a spreadsheet would run as a formula (CSV / formula injection).
    ///     When on, a text value that starts with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, a tab or a carriage
    ///     return is written with a leading <c>'</c>, so Excel, LibreOffice and Google Sheets show it as text
    ///     instead of evaluating it. Numbers, dates and other formatted values are never changed, so a negative
    ///     number stays a number. Default true; turn it off only when the file is never opened in a spreadsheet
    ///     and the exact text matters.
    /// </summary>
    public bool EscapeFormulas { get; set; } = true;
}

/// <summary>Options for the JSON exporter.</summary>
public class JsonOptions
{
    private JsonSerializerOptions? _effective;
    private bool _writeIndented;
    private JsonSerializerOptions? _serializerOptions;

    /// <summary>Whether to indent the JSON. Ignored when <see cref="SerializerOptions" /> is set.</summary>
    public bool WriteIndented
    {
        get => _writeIndented;
        set
        {
            _writeIndented = value;
            _effective = null;
        }
    }

    /// <summary>Explicit serializer options; when set, used as-is.</summary>
    public JsonSerializerOptions? SerializerOptions
    {
        get => _serializerOptions;
        set
        {
            _serializerOptions = value;
            _effective = null;
        }
    }

    /// <summary>
    ///     The options actually used: caller-supplied, else one derived from <see cref="WriteIndented" />.
    /// </summary>
    /// <remarks>
    ///     The derived instance is built once and reused. System.Text.Json caches its serialization metadata
    ///     per <see cref="JsonSerializerOptions" /> instance, so handing it a fresh instance on every export -
    ///     as this used to - rebuilt that cache every time and made each export pay full reflection cost.
    /// </remarks>
    public JsonSerializerOptions Effective =>
        _effective ??= SerializerOptions ?? new JsonSerializerOptions { WriteIndented = WriteIndented };
}

/// <summary>Options for the TXT exporter.</summary>
public class TxtOptions
{
    /// <summary>Text encoding. Default UTF-8 without BOM.</summary>
    public Encoding Encoding { get; set; } = new UTF8Encoding(false);

    /// <summary>Line terminator. Default <see cref="Environment.NewLine" />.</summary>
    public string NewLine { get; set; } = Environment.NewLine;
}

/// <summary>Options for the MessagePack exporter.</summary>
public class MessagePackOptions
{
    /// <summary>Explicit serializer options; when set, used as-is.</summary>
    public MessagePackSerializerOptions? SerializerOptions { get; set; }

    /// <summary>The options actually used: caller-supplied, else contractless standard (no attributes required).</summary>
    public MessagePackSerializerOptions Effective =>
        SerializerOptions ?? MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);
}

/// <summary>Aggregate options for the core exporters, configured via <c>AddExport</c>.</summary>
public class ExportOptions
{
    /// <summary>CSV options.</summary>
    public CsvOptions Csv { get; set; } = new();

    /// <summary>JSON options.</summary>
    public JsonOptions Json { get; set; } = new();

    /// <summary>TXT options.</summary>
    public TxtOptions Txt { get; set; } = new();

    /// <summary>MessagePack options.</summary>
    public MessagePackOptions MessagePack { get; set; } = new();
}
