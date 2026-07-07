using System;
using ArturRios.Data.MongoDb;
using EphemeralMongo;
using MongoDB.Driver;

namespace ArturRios.Data.Tests.MongoDb.TestSupport;

/// <summary>
///     Starts a single ephemeral MongoDB single-node replica set for the whole Mongo test collection
///     (replica set is required for transactions). Each call to <see cref="NewContext" /> targets a fresh,
///     uniquely-named database so tests are isolated.
/// </summary>
public sealed class MongoReplicaSetFixture : IDisposable
{
    private readonly IMongoRunner _runner;

    public MongoReplicaSetFixture() =>
        _runner = MongoRunner.Run(new MongoRunnerOptions { UseSingleNodeReplicaSet = true });

    public string ConnectionString => _runner.ConnectionString;

    public void Dispose() => _runner.Dispose();

    public IMongoClient CreateClient() => new MongoClient(_runner.ConnectionString);

    public MongoContext NewContext(out IMongoClient client)
    {
        client = CreateClient();
        var database = client.GetDatabase("test_" + Guid.NewGuid().ToString("N"));
        return new MongoContext(database);
    }

    public MongoContext NewContext() => NewContext(out _);
}
