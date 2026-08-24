using ArturRios.Data.DynamoDb.Configuration;

namespace ArturRios.Data.Tests.DynamoDb;

[Trait("Category", "Unit")]
public class DynamoOptionsTests
{
    [Fact]
    public void GivenAnObjectInitializer_WhenSettingTheDynamoOptions_ThenEveryValueIsCarried()
    {
        var o = new DynamoOptions
        {
            Region = "us-east-1", ServiceUrl = "http://localhost:8000", AccessKey = "ak", SecretKey = "sk"
        };

        Assert.Equal("us-east-1", o.Region);
        Assert.Equal("http://localhost:8000", o.ServiceUrl);
        Assert.Equal("ak", o.AccessKey);
        Assert.Equal("sk", o.SecretKey);
    }

    [Fact]
    public void GivenNewDynamoOptions_WhenInspected_ThenTheServiceUrlAndCredentialsDefaultToNull()
    {
        var o = new DynamoOptions { Region = "us-east-1" };
        Assert.Null(o.ServiceUrl);
        Assert.Null(o.AccessKey);
        Assert.Null(o.SecretKey);
    }
}
