using ArturRios.Data.DynamoDb.Configuration;

namespace ArturRios.Data.Tests.DynamoDb;

public class DynamoOptionsTests
{
    [Fact]
    public void Options_CarryRegionServiceUrlAndCredentials()
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
    public void Options_ServiceUrlAndCredentials_DefaultToNull()
    {
        var o = new DynamoOptions { Region = "us-east-1" };
        Assert.Null(o.ServiceUrl);
        Assert.Null(o.AccessKey);
        Assert.Null(o.SecretKey);
    }
}
