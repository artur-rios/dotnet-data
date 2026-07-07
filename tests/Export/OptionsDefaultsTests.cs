using System.Text;
using ArturRios.Data.Export.Configuration;

namespace ArturRios.Data.Tests.Export;

public class OptionsDefaultsTests
{
    [Fact]
    public void ExportOptions_HaveExpectedDefaults()
    {
        var options = new ExportOptions();

        Assert.Equal(',', options.Csv.Delimiter);
        Assert.True(options.Csv.IncludeHeader);
        Assert.IsType<UTF8Encoding>(options.Csv.Encoding);
        Assert.False(options.Json.WriteIndented);
        Assert.Null(options.Json.SerializerOptions);
        Assert.Equal(Environment.NewLine, options.Txt.NewLine);
        Assert.NotNull(options.MessagePack.Effective);
    }

    [Fact]
    public void CsvEncoding_IsUtf8WithoutBom()
    {
        var preamble = new ExportOptions().Csv.Encoding.GetPreamble();
        Assert.Empty(preamble);
    }
}
