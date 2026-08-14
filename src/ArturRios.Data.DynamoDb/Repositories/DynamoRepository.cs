using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using System.Runtime.CompilerServices;
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Output;
using Microsoft.Extensions.Logging;

namespace ArturRios.Data.DynamoDb.Repositories;

/// <summary>
///     DynamoDB implementation of <see cref="IAsyncDynamoRepository{T}" /> over the AWS object-persistence
///     model (<see cref="IDynamoDBContext" />). Failures are returned as <see cref="DataOutput{T}" /> /
///     <see cref="ProcessOutput" />; a <see cref="ConditionalCheckFailedException" /> (from
///     <c>[DynamoDBVersion]</c> optimistic locking) becomes a concurrency error.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="context">The DynamoDB object-persistence context.</param>
/// <param name="logger">
///     Optional logger. Envelopes never carry service text, so a failure is otherwise
///     undiagnosable: supply a logger and the full exception, plus the item type and the
///     repository method that failed, is written at <see cref="LogLevel.Error" />. Item contents
///     and key values are never logged. Resolved from DI when logging is registered.
/// </param>
public class DynamoRepository<T>(IDynamoDBContext context, ILogger<DynamoRepository<T>>? logger = null)
    : IAsyncDynamoRepository<T> where T : class
{
    /// <summary>Message returned when an operation fails with no finer classification.</summary>
    protected const string OperationFailedMessage = "A data-access error occurred.";

    /// <summary>Message returned on an optimistic-concurrency conflict.</summary>
    protected const string ConcurrencyMessage = "Concurrency conflict: the item was modified by another process.";

    /// <summary>Message returned when the failure is transient and the operation may be retried.</summary>
    protected const string TransientMessage = "The data store is temporarily unavailable. Please retry.";

    /// <summary>
    ///     The DynamoDB batch-write API rejects types with a <c>[DynamoDBVersion]</c> property unless
    ///     version checking is explicitly skipped (batch writes have no per-item conditional-check
    ///     support). Optimistic concurrency remains enforced on the single-item <see cref="SaveAsync" />/
    ///     <see cref="DeleteAsync" /> paths.
    /// </summary>
    private static readonly BatchWriteConfig BatchSkipVersionCheckConfig = new() { SkipVersionCheck = true };

    /// <inheritdoc />
    public Task<DataOutput<T>> SaveAsync(T item, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            await context.SaveAsync(item, ct);

            return item;
        });

    /// <inheritdoc />
    public Task<DataOutput<T?>> LoadAsync(object hashKey, CancellationToken ct = default) =>
        GuardedAsync<T?>(async () => await context.LoadAsync<T>(hashKey, ct));

    /// <inheritdoc />
    public Task<DataOutput<T?>> LoadAsync(object hashKey, object rangeKey, CancellationToken ct = default) =>
        GuardedAsync<T?>(async () => await context.LoadAsync<T>(hashKey, rangeKey, ct));

    /// <inheritdoc />
    public Task<ProcessOutput> DeleteAsync(T item, CancellationToken ct = default) =>
        GuardedProcessAsync(async () => await context.DeleteAsync(item, ct));

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> QueryAsync(object hashKey, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () => await context.QueryAsync<T>(hashKey).GetRemainingAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> QueryAsync(object hashKey, QueryOperator op,
        IEnumerable<object> sortKeyValues, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () =>
            await context.QueryAsync<T>(hashKey, op, sortKeyValues).GetRemainingAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> ScanAsync(IEnumerable<ScanCondition> conditions,
        CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () => await context.ScanAsync<T>(conditions).GetRemainingAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> SaveManyAsync(IEnumerable<T> items, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () =>
        {
            var list = items.ToList();
            var batch = context.CreateBatchWrite<T>(BatchSkipVersionCheckConfig);
            batch.AddPutItems(list);
            await batch.ExecuteAsync(ct);
            return list;
        });

    /// <inheritdoc />
    public Task<ProcessOutput> DeleteManyAsync(IEnumerable<T> items, CancellationToken ct = default) =>
        GuardedProcessAsync(async () =>
        {
            var batch = context.CreateBatchWrite<T>(BatchSkipVersionCheckConfig);
            batch.AddDeleteItems(items.ToList());
            await batch.ExecuteAsync(ct);
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>>
        LoadManyAsync(IEnumerable<object> hashKeys, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () =>
        {
            var batch = context.CreateBatchGet<T>();
            foreach (var key in hashKeys)
            {
                batch.AddKey(key);
            }

            await batch.ExecuteAsync(ct);

            return batch.Results;
        });

    /// <summary>Runs an operation returning data, converting failures to envelope errors.</summary>
    /// <param name="operation">The operation to run.</param>
    /// <param name="operationName">The calling repository method, used as log context.</param>
    protected async Task<DataOutput<TResult>> GuardedAsync<TResult>(Func<Task<TResult>> operation,
        [CallerMemberName] string operationName = "")
    {
        try
        {
            return DataOutput<TResult>.New.WithData(await operation());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail<TResult>(ex, operationName);
        }
    }

    /// <summary>Runs an operation with no payload, converting failures to envelope errors.</summary>
    /// <param name="operation">The operation to run.</param>
    /// <param name="operationName">The calling repository method, used as log context.</param>
    protected async Task<ProcessOutput> GuardedProcessAsync(Func<Task> operation,
        [CallerMemberName] string operationName = "")
    {
        try
        {
            await operation();
            return ProcessOutput.New;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log(ex, operationName);

            return ex is ConditionalCheckFailedException
                ? ProcessOutput.New.WithError(ConcurrencyMessage)
                : ProcessOutput.New.WithError(Describe(ex));
        }
    }

    /// <summary>
    ///     Logs the failure in full when a logger is configured, and maps it to a data-output error
    ///     envelope. Service text names tables, indexes, keys and request ids, so it goes to the
    ///     log and never to the caller.
    /// </summary>
    /// <param name="ex">The exception caught by a guard.</param>
    /// <param name="operationName">The repository method that failed, used as log context.</param>
    protected DataOutput<TResult> Fail<TResult>(Exception ex, string operationName = "")
    {
        Log(ex, operationName);

        return ex switch
        {
            ConditionalCheckFailedException => DataOutput<TResult>.New.WithError(ConcurrencyMessage),
            _ => DataOutput<TResult>.New.WithError(Describe(ex))
        };
    }

    private void Log(Exception ex, string operationName)
    {
        // A conditional-check failure is the expected outcome of optimistic locking, not an
        // operational fault: logging it at Error would fill the log with routine contention.
        var level = ex is ConditionalCheckFailedException ? LogLevel.Debug : LogLevel.Error;

        logger?.Log(level, ex, "DynamoDB operation failed. Item: {Item}, operation: {Operation}",
            typeof(T).Name, operationName);
    }

    /// <summary>Classifies a non-concurrency failure into a caller-safe message.</summary>
    protected static string Describe(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException || IsRetryableServiceFault(current))
            {
                return TransientMessage;
            }
        }

        return OperationFailedMessage;
    }

    // Throttling and server faults reach the caller under several exception types (and as a plain
    // AmazonDynamoDBException with a throttling error code), so classify on what the SDK reports
    // about the response rather than on the exception type alone.
    private static bool IsRetryableServiceFault(Exception ex)
    {
        if (ex is ProvisionedThroughputExceededException or RequestLimitExceededException
            or InternalServerErrorException)
        {
            return true;
        }

        return ex is AmazonServiceException service &&
               (service.Retryable is not null ||
                (int)service.StatusCode == 429 ||
                (int)service.StatusCode >= 500);
    }
}
