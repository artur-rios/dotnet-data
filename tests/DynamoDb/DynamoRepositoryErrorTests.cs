using System;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using ArturRios.Data.DynamoDb.Repositories;
using ArturRios.Data.Tests.DynamoDb.TestSupport;
using ArturRios.Data.Tests.TestSupport;
using ArturRios.Output;
using Microsoft.Extensions.Logging;

namespace ArturRios.Data.Tests.DynamoDb;

/// <summary>
///     How the DynamoDB repository turns a failure into an envelope, pinned against the SDK's own exception
///     types without a server: a conditional-check failure is a concurrency conflict logged at Debug,
///     throttling and server faults are transient, anything else is generic, and cancellation propagates.
/// </summary>
[Trait("Category", "Unit")]
public class DynamoRepositoryErrorTests
{
    private const string Concurrency = "Concurrency conflict: the item was modified or removed by another process.";
    private const string Transient = "The data store is temporarily unavailable. Please retry.";
    private const string Generic = "A data-access error occurred.";

    public static TheoryData<Exception, string> Failures => new()
    {
        { new ConditionalCheckFailedException("The conditional request failed"), Concurrency },
        { new ProvisionedThroughputExceededException("throughput exceeded on table TestItems"), Transient },
        { new RequestLimitExceededException("account limit"), Transient },
        { new InternalServerErrorException("internal"), Transient },
        { ServiceFault(HttpStatusCode.TooManyRequests), Transient },
        { ServiceFault(HttpStatusCode.ServiceUnavailable), Transient },
        { ServiceFault(HttpStatusCode.BadRequest), Generic },
        { new InvalidOperationException("outer", new TimeoutException("slow")), Transient },
        { new InvalidOperationException("table TestItems in us-east-1 not found"), Generic }
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task GivenAFailure_WhenAnOperationReturningDataFails_ThenItIsClassified(Exception failure,
        string expected)
    {
        var result = await NewRepo().Run(() => throw failure);

        Assert.False(result.Success);
        Assert.Equal([expected], result.Errors);
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task GivenAFailure_WhenAnOperationWithNoPayloadFails_ThenItIsClassified(Exception failure,
        string expected)
    {
        var result = await NewRepo().RunProcess(() => throw failure);

        Assert.False(result.Success);
        Assert.Equal([expected], result.Errors);
    }

    [Fact]
    public async Task GivenAConditionalCheckFailure_WhenItIsLogged_ThenItIsLoggedAtDebugWithTheOperationName()
    {
        var logger = new ListLogger<DynamoRepository<TestItem>>();

        await NewRepo(logger).RunProcess(() => throw new ConditionalCheckFailedException("stale"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains(nameof(ProbeRepository.RunProcess), entry.Message);
        Assert.Contains(nameof(TestItem), entry.Message);
    }

    [Fact]
    public async Task GivenAnOperationalFailure_WhenItIsLogged_ThenItIsLoggedAtErrorWithTheException()
    {
        var logger = new ListLogger<DynamoRepository<TestItem>>();
        var failure = new InternalServerErrorException("internal");

        await NewRepo(logger).Run(() => throw failure);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(failure, entry.Exception);
    }

    [Fact]
    public async Task GivenACancelledOperation_WhenItRuns_ThenCancellationPropagatesInsteadOfBeingEnveloped()
    {
        var repo = NewRepo();

        await Assert.ThrowsAsync<OperationCanceledException>(() => repo.Run(() => throw new OperationCanceledException()));
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repo.RunProcess(() => throw new OperationCanceledException()));
    }

    [Fact]
    public async Task GivenAnOperationThatSucceeds_WhenItRuns_ThenItsResultComesBack()
    {
        var repo = NewRepo();

        Assert.Equal(42, (await repo.Run(() => Task.FromResult(42))).Data);
        Assert.True((await repo.RunProcess(() => Task.CompletedTask)).Success);
    }

    private static AmazonDynamoDBException ServiceFault(HttpStatusCode status) =>
        new("service fault", ErrorType.Unknown, "SomeCode", "request-1", status);

    private static ProbeRepository NewRepo(ILogger<DynamoRepository<TestItem>>? logger = null) =>
        new(InterfaceStub.For<IDynamoDBContext>((_, _) => (false, null)), logger);

    // Exposes the repository's guards, so the classification is exercised exactly as the public methods use it.
    private sealed class ProbeRepository(IDynamoDBContext context, ILogger<DynamoRepository<TestItem>>? logger)
        : DynamoRepository<TestItem>(context, logger)
    {
        public Task<DataOutput<int>> Run(Func<Task<int>> operation) => GuardedAsync(operation);

        public Task<ProcessOutput> RunProcess(Func<Task> operation) => GuardedProcessAsync(operation);
    }
}
