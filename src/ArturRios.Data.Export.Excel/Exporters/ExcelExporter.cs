using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.Excel.Configuration;
using ArturRios.Data.Export.Exporters;
using ClosedXML.Excel;

namespace ArturRios.Data.Export.Excel.Exporters;

/// <summary>Writes records to a single-worksheet .xlsx workbook using ClosedXML and the shared column map.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">Excel options.</param>
public class ExcelExporter<T>(ExcelExportOptions options) : ExporterBase<T> where T : class
{
    /// <inheritdoc />
    protected override Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct)
    {
        var columns = ColumnMap.For<T>();
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(options.SheetName);
        var row = 1;

        if (options.IncludeHeader)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                var cell = worksheet.Cell(row, i + 1);
                cell.Value = columns[i].Header;
                if (options.BoldHeader) cell.Style.Font.Bold = true;
            }

            row++;
        }

        foreach (var item in data)
        {
            ct.ThrowIfCancellationRequested();
            for (var i = 0; i < columns.Count; i++)
            {
                SetCell(worksheet.Cell(row, i + 1), columns[i].Getter(item));
            }

            row++;
        }

        if (options.AutoFitColumns && columns.Count > 0)
        {
            worksheet.Columns().AdjustToContents();
        }

        workbook.SaveAs(destination);
        return Task.CompletedTask;
    }

    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case bool b:
                cell.Value = b;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                cell.Value = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                break;
            default:
                cell.Value = ValueRenderer.Render(value);
                break;
        }
    }
}
