using System.Globalization;
using ArturRios.Data.Export.Abstractions;

namespace ArturRios.Data.Tests.Export;

public class ValueRendererTests
{
    [Fact]
    public void Render_Null_ReturnsEmpty() => Assert.Equal(string.Empty, ValueRenderer.Render(null));

    [Fact]
    public void Render_String_ReturnsItself() => Assert.Equal("hello", ValueRenderer.Render("hello"));

    [Fact]
    public void Render_Decimal_UsesInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // comma decimal separator
        try { Assert.Equal("1234.5", ValueRenderer.Render(1234.5m)); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Render_DateTime_UsesInvariantCulture()
    {
        var value = new DateTime(2026, 7, 7, 13, 5, 0, DateTimeKind.Unspecified);
        Assert.Equal(value.ToString(CultureInfo.InvariantCulture), ValueRenderer.Render(value));
    }
}
