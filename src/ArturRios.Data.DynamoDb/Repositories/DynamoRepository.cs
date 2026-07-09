using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.DynamoDb.Repositories;

/// <summary>
///     DynamoDB implementation of <see cref="IAsyncDynamoRepository{T}" /> over the AWS object-persistence
///     model (<see cref="IDynamoDBContext" />). Failures are returned as <see cref="DataOutput{T}" /> /
///     <see cref="ProcessOutput" />; a <see cref="ConditionalCheckFailedException" /> (from
///     <c>[DynamoDBVersion]</c> optimistic locking) becomes a concurrency error.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="context">The DynamoDB object-persistence context.</param>
public class DynamoRepository<T>(IDynamoDBContext context) : IAsyncDynamoRepository<T> where T : class
{
    /// <summary>Message prefix returned when an operation fails.</summary>
    protected const string OperationFailedMessage = "A data-access error occurred:";

    /// <summary>Message returned on an optimistic-concurrency conflict.</summary>
    protected const string ConcurrencyMessage = "Concurrency conflict: the item was modified by another process.";

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
    protected static async Task<DataOutput<TResult>> GuardedAsync<TResult>(Func<Task<TResult>> operation)
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
            return Fail<TResult>(ex);
        }
    }

    /// <summary>Runs an operation with no payload, converting failures to envelope errors.</summary>
    protected static async Task<ProcessOutput> GuardedProcessAsync(Func<Task> operation)
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
            return ex is ConditionalCheckFailedException
                ? ProcessOutput.New.WithError(ConcurrencyMessage)
                : ProcessOutput.New.WithError($"{OperationFailedMessage} {ex.GetBaseException().Message}");
        }
    }

    /// <summary>Maps an exception to a data-output error envelope.</summary>
    protected static DataOutput<TResult> Fail<TResult>(Exception ex) => ex switch
    {
        ConditionalCheckFailedException => DataOutput<TResult>.New.WithError(ConcurrencyMessage),
        _ => DataOutput<TResult>.New.WithError($"{OperationFailedMessage} {ex.GetBaseException().Message}")
    };
}
