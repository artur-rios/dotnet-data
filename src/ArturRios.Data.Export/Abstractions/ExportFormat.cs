namespace ArturRios.Data.Export.Abstractions;

/// <summary>The supported export formats. <see cref="Excel" /> requires the ArturRios.Data.Export.Excel package.</summary>
public enum ExportFormat
{
    /// <summary>Comma-separated values.</summary>
    Csv,

    /// <summary>JSON array.</summary>
    Json,

    /// <summary>Plain text, one line per record.</summary>
    Txt,

    /// <summary>MessagePack binary.</summary>
    MessagePack,

    /// <summary>Excel .xlsx. Requires ArturRios.Data.Export.Excel and <c>AddExcelExport()</c>.</summary>
    Excel
}
