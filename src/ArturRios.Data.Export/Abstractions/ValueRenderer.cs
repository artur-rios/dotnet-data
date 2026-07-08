using System.Globalization;

namespace ArturRios.Data.Export.Abstractions;

/// <summary>Renders a value to a stable, culture-invariant string for columnar/text output.</summary>
public static class ValueRenderer
{
    /// <summary>null → empty; <see cref="IFormattable" /> → invariant culture; otherwise <see cref="object.ToString" />.</summary>
    public static string Render(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}
