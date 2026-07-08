namespace ArturRios.Data.Export.Excel.Configuration;

/// <summary>Options for the Excel exporter.</summary>
public class ExcelExportOptions
{
    /// <summary>Worksheet name. Default "Sheet1".</summary>
    public string SheetName { get; set; } = "Sheet1";

    /// <summary>Whether to write a header row from the column map. Default true.</summary>
    public bool IncludeHeader { get; set; } = true;

    /// <summary>Whether the header row is bold. Default true.</summary>
    public bool BoldHeader { get; set; } = true;

    /// <summary>Whether to auto-fit column widths. Default true.</summary>
    public bool AutoFitColumns { get; set; } = true;
}
