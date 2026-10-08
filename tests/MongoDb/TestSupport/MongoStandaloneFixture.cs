using System;
using ArturRios.Data.MongoDb;
using EphemeralMongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ArturRios.Data.Tests.MongoDb.TestSupport;

/// <summary>
///     Starts an ephemeral standalone MongoDB server (no replica set, so no transaction support). Each call to
///     <see cref="NewContext" /> targets a fresh, uniquely-named database on a client that has already
///     discovered the server is standalone.
/// </summary>
public sealed class MongoStandaloneFixture : IDisposable
{
    private readonly IMongoRunner _runner = MongoRunner.Run(new MongoRunnerOptions { UseSingleNodeReplicaSet = false });

    public void Dispose() => _runner.Dispose();

    public MongoContext NewContext(out IMongoClient client)
    {
        client = new MongoClient(_runner.ConnectionString);
        client.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument("ping", 1));
        return new MongoContext(client.GetDatabase("test_" + Guid.NewGuid().ToString("N")));
    }
}
