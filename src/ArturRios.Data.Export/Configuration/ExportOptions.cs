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
}

/// <summary>Options for the JSON exporter.</summary>
public class JsonOptions
{
    /// <summary>Whether to indent the JSON. Ignored when <see cref="SerializerOptions" /> is set.</summary>
    public bool WriteIndented { get; set; }

    /// <summary>Explicit serializer options; when set, used as-is.</summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }
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
