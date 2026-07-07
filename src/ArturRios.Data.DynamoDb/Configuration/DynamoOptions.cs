namespace ArturRios.Data.DynamoDb.Configuration;

/// <summary>Connection options for the DynamoDB store.</summary>
public class DynamoOptions
{
    /// <summary>AWS region system name (e.g. "us-east-1"). Ignored when <see cref="ServiceUrl" /> is set.</summary>
    public string Region { get; init; } = string.Empty;

    /// <summary>Optional service URL for DynamoDB Local / LocalStack (e.g. "http://localhost:8000").</summary>
    public string? ServiceUrl { get; init; }

    /// <summary>Optional explicit AWS access key. When <see cref="ServiceUrl" /> is set and unset, dummy credentials are used.</summary>
    public string? AccessKey { get; init; }

    /// <summary>Optional explicit AWS secret key.</summary>
    public string? SecretKey { get; init; }
}
