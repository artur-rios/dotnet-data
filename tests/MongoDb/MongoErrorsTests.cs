using System;
using System.Net;
using ArturRios.Data.MongoDb.Exceptions;
using ArturRios.Data.MongoDb.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace ArturRios.Data.Tests.MongoDb;

/// <summary>
///     The classification behind every Mongo error envelope, pinned against the driver's own exception
///     types: duplicate keys by server code, transient faults by type, anywhere in the exception chain.
/// </summary>
[Trait("Category", "Unit")]
public class MongoErrorsTests
{
    private static readonly ConnectionId Connection =
        new(new ServerId(new ClusterId(), new DnsEndPoint("cluster0.internal", 27017)));

    [Fact]
    public void GivenAConcurrencyException_WhenDescribed_ThenTheConcurrencyMessageComesBack() =>
        Assert.Equal(MongoErrors.ConcurrencyMessage, MongoErrors.Describe(new MongoConcurrencyException()));

    [Theory]
    [InlineData(11000)]
    [InlineData(11001)]
    public void GivenADuplicateKeyCommandFailure_WhenDescribed_ThenTheUniqueMessageComesBack(int code) =>
        Assert.Equal(MongoErrors.UniqueViolationMessage, MongoErrors.Describe(CommandFailure(code)));

    [Fact]
    public void GivenADuplicateKeyNestedInTheChain_WhenCheckingForAUniqueViolation_ThenItIsFound() =>
        Assert.True(MongoErrors.IsUniqueViolation(new InvalidOperationException("outer", CommandFailure(11000))));

    [Fact]
    public void GivenACommandFailureWithAnotherCode_WhenDescribed_ThenTheGenericMessageComesBack()
    {
        var failure = CommandFailure(2);

        Assert.False(MongoErrors.IsUniqueViolation(failure));
        Assert.Equal(MongoErrors.GenericMessage, MongoErrors.Describe(failure));
    }

    public static TheoryData<Exception> TransientFailures => new()
    {
        new MongoConnectionException(Connection, "connection reset"),
        new MongoExecutionTimeoutException(Connection, "operation exceeded time limit"),
        new MongoNotPrimaryException(Connection, new BsonDocument("insert", "items"),
            new BsonDocument { { "ok", 0 }, { "code", 10107 } }),
        new TimeoutException("server selection timed out"),
        new InvalidOperationException("outer", new MongoConnectionException(Connection, "inner"))
    };

    [Theory]
    [MemberData(nameof(TransientFailures))]
    public void GivenATransientFailure_WhenDescribed_ThenTheTransientMessageComesBack(Exception failure)
    {
        Assert.True(MongoErrors.IsTransient(failure));
        Assert.Equal(MongoErrors.TransientMessage, MongoErrors.Describe(failure));
    }

    [Fact]
    public void GivenAFailureThatIsNeitherUniqueNorTransient_WhenDescribed_ThenTheGenericMessageComesBack()
    {
        var failure = new InvalidOperationException("index items_name_1 on cluster0.internal:27017");

        Assert.False(MongoErrors.IsTransient(failure));
        Assert.Equal(MongoErrors.GenericMessage, MongoErrors.Describe(failure));
    }

    private static MongoCommandException CommandFailure(int code) =>
        new(Connection, "command failed", new BsonDocument("update", "items"),
            new BsonDocument { { "ok", 0 }, { "code", code }, { "errmsg", "E11000 duplicate key" } });
}
