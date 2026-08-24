using System.Globalization;
using ArturRios.Data.Export.Abstractions;

namespace ArturRios.Data.Tests.Export;

[Trait("Category", "Unit")]
public class ValueRendererTests
{
    [Fact]
    public void GivenNull_WhenRendering_ThenAnEmptyStringComesBack() => Assert.Equal(string.Empty, ValueRenderer.Render(null));

    [Fact]
    public void GivenAString_WhenRendering_ThenItComesBackUnchanged() => Assert.Equal("hello", ValueRenderer.Render("hello"));

    [Fact]
    public void GivenADecimal_WhenRendering_ThenTheInvariantCultureIsUsed()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // comma decimal separator
        try { Assert.Equal("1234.5", ValueRenderer.Render(1234.5m)); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void GivenADateTime_WhenRendering_ThenTheInvariantCultureIsUsed()
    {
        var value = new DateTime(2026, 7, 7, 13, 5, 0, DateTimeKind.Unspecified);
        Assert.Equal(value.ToString(CultureInfo.InvariantCulture), ValueRenderer.Render(value));
    }
}
