using System.Text;
using ArturRios.Data.Export.Configuration;

namespace ArturRios.Data.Tests.Export;

[Trait("Category", "Unit")]
public class OptionsDefaultsTests
{
    [Fact]
    public void GivenNewExportOptions_WhenInspected_ThenEverySectionCarriesItsDocumentedDefaults()
    {
        var options = new ExportOptions();

        Assert.Equal(',', options.Csv.Delimiter);
        Assert.True(options.Csv.IncludeHeader);
        Assert.IsType<UTF8Encoding>(options.Csv.Encoding);
        Assert.True(options.Csv.EscapeFormulas);
        Assert.False(options.Json.WriteIndented);
        Assert.Null(options.Json.SerializerOptions);
        Assert.Equal(Environment.NewLine, options.Txt.NewLine);
        Assert.NotNull(options.MessagePack.Effective);
    }

    [Fact]
    public void GivenNewCsvOptions_WhenInspected_ThenTheEncodingIsUtf8WithoutABom()
    {
        var preamble = new ExportOptions().Csv.Encoding.GetPreamble();
        Assert.Empty(preamble);
    }
    [Fact]
    public void GivenExplicitJsonSerializerOptions_WhenTheEffectiveOptionsAreRead_ThenTheyAreUsedAsIs()
    {
        var explicitOptions = new System.Text.Json.JsonSerializerOptions();
        var options = new ArturRios.Data.Export.Configuration.JsonOptions { WriteIndented = true };
        var derived = options.Effective;

        options.SerializerOptions = explicitOptions;

        Assert.True(derived.WriteIndented);
        Assert.Same(explicitOptions, options.Effective);
    }

    [Fact]
    public void GivenTheJsonIndentationChanges_WhenTheEffectiveOptionsAreRead_ThenTheyAreRebuiltOnceAndReused()
    {
        var options = new ArturRios.Data.Export.Configuration.JsonOptions();
        var compact = options.Effective;

        options.WriteIndented = true;
        var indented = options.Effective;

        Assert.False(compact.WriteIndented);
        Assert.True(indented.WriteIndented);
        Assert.Same(indented, options.Effective);
    }

    [Fact]
    public void GivenExplicitMessagePackSerializerOptions_WhenTheEffectiveOptionsAreRead_ThenTheyAreUsedAsIs()
    {
        var explicitOptions = MessagePack.MessagePackSerializerOptions.Standard;
        var options = new ArturRios.Data.Export.Configuration.MessagePackOptions { SerializerOptions = explicitOptions };

        Assert.Same(explicitOptions, options.Effective);
    }
}
