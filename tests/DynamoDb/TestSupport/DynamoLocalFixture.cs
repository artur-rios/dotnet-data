#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace ArturRios.Data.Tests.DynamoDb.TestSupport;

/// <summary>
/// Downloads DynamoDB Local once, runs one in-memory instance (Java) for the whole Dynamo test
/// collection, and exposes a client/context factory plus a table-creation helper. Disposing kills
/// the Java process.
/// </summary>
public sealed class DynamoLocalFixture : IDisposable
{
    private const string DownloadUrl = "https://s3.us-west-2.amazonaws.com/dynamodb-local/dynamodb_local_latest.zip";
    private readonly Process _process;
    public string ServiceUrl { get; }

    public DynamoLocalFixture()
    {
        var dir = EnsureDynamoDbLocal();
        var port = FreeTcpPort();
        ServiceUrl = $"http://localhost:{port}";
        _process = StartJava(dir, port);
        WaitUntilReady().GetAwaiter().GetResult();
    }

    public IAmazonDynamoDB CreateClient() =>
        new AmazonDynamoDBClient(new BasicAWSCredentials("dummy", "dummy"),
            new AmazonDynamoDBConfig { ServiceURL = ServiceUrl, AuthenticationRegion = "us-east-1" });

    public IDynamoDBContext CreateContext() =>
        new DynamoDBContextBuilder().WithDynamoDBClient(CreateClient).Build();

    public async Task CreateTableAsync(string tableName, string hashKey, string? rangeKey = null)
    {
        using var client = CreateClient();
        var attrs = new List<AttributeDefinition> { new(hashKey, ScalarAttributeType.S) };
        var schema = new List<KeySchemaElement> { new(hashKey, KeyType.HASH) };
        if (rangeKey is not null)
        {
            attrs.Add(new AttributeDefinition(rangeKey, ScalarAttributeType.S));
            schema.Add(new KeySchemaElement(rangeKey, KeyType.RANGE));
        }
        try
        {
            await client.CreateTableAsync(new CreateTableRequest
            {
                TableName = tableName,
                AttributeDefinitions = attrs,
                KeySchema = schema,
                BillingMode = BillingMode.PAY_PER_REQUEST
            });
        }
        catch (ResourceInUseException)
        {
            // Table already exists (e.g. a prior test class instance in the same shared fixture
            // already created it). Table creation is treated as idempotent for test setup.
        }
        // wait until ACTIVE
        for (var i = 0; i < 50; i++)
        {
            var desc = await client.DescribeTableAsync(tableName);
            if (desc.Table.TableStatus == TableStatus.ACTIVE) return;
            await Task.Delay(100);
        }
    }

    private static string EnsureDynamoDbLocal()
    {
        var cache = Path.Combine(Path.GetTempPath(), "dynamodb-local-cache");
        var jar = Path.Combine(cache, "DynamoDBLocal.jar");
        if (File.Exists(jar)) return cache;
        Directory.CreateDirectory(cache);
        var zip = Path.Combine(cache, "ddb.zip");
        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
        using (var s = http.GetStreamAsync(DownloadUrl).GetAwaiter().GetResult())
        using (var f = File.Create(zip))
            s.CopyTo(f);
        ZipFile.ExtractToDirectory(zip, cache, overwriteFiles: true);
        return cache;
    }

    private static Process StartJava(string dir, int port)
    {
        var java = FindJava();
        var psi = new ProcessStartInfo(java)
        {
            WorkingDirectory = dir,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        psi.ArgumentList.Add($"-Djava.library.path={Path.Combine(dir, "DynamoDBLocal_lib")}");
        psi.ArgumentList.Add("-jar");
        psi.ArgumentList.Add(Path.Combine(dir, "DynamoDBLocal.jar"));
        psi.ArgumentList.Add("-inMemory");
        psi.ArgumentList.Add("-port");
        psi.ArgumentList.Add(port.ToString());
        var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start DynamoDB Local (java).");
        return p;
    }

    private static string FindJava()
    {
        var home = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(home))
        {
            var candidate = Path.Combine(home, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java");
            if (File.Exists(candidate)) return candidate;
        }
        return OperatingSystem.IsWindows() ? "java.exe" : "java";
    }

    private async Task WaitUntilReady()
    {
        using var client = CreateClient();
        for (var i = 0; i < 100; i++)
        {
            try { await client.ListTablesAsync(); return; }
            catch { await Task.Delay(200); }
        }
        throw new InvalidOperationException("DynamoDB Local did not become ready in time.");
    }

    private static int FreeTcpPort()
    {
        var l = new TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    public void Dispose()
    {
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); } catch { /* ignore */ }
        _process.Dispose();
    }
}
