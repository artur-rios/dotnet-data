using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using ArturRios.Output;

namespace ArturRios.Data.DynamoDb.Interfaces;

/// <summary>
/// Asynchronous DynamoDB repository over the AWS object-persistence model. All results are
/// enveloped in <see cref="DataOutput{T}"/> / <see cref="ProcessOutput"/>.
/// </summary>
/// <typeparam name="T">The item type (a POCO annotated with DynamoDB attributes).</typeparam>
public interface IAsyncDynamoRepository<T> where T : class
{
    /// <summary>Puts (creates or replaces) an item and returns it.</summary>
    Task<DataOutput<T>> SaveAsync(T item, CancellationToken ct = default);

    /// <summary>Loads an item by partition key, or a successful null when not found.</summary>
    Task<DataOutput<T?>> LoadAsync(object hashKey, CancellationToken ct = default);

    /// <summary>Loads an item by partition and sort key, or a successful null when not found.</summary>
    Task<DataOutput<T?>> LoadAsync(object hashKey, object rangeKey, CancellationToken ct = default);

    /// <summary>Deletes the given item (idempotent).</summary>
    Task<ProcessOutput> DeleteAsync(T item, CancellationToken ct = default);

    /// <summary>Returns all items with the given partition key.</summary>
    Task<DataOutput<IEnumerable<T>>> QueryAsync(object hashKey, CancellationToken ct = default);

    /// <summary>Returns items with the given partition key and a sort-key condition.</summary>
    Task<DataOutput<IEnumerable<T>>> QueryAsync(object hashKey, QueryOperator op, IEnumerable<object> sortKeyValues, CancellationToken ct = default);

    /// <summary>Scans the table with the given conditions. This is a full-table scan — use sparingly.</summary>
    Task<DataOutput<IEnumerable<T>>> ScanAsync(IEnumerable<ScanCondition> conditions, CancellationToken ct = default);

    /// <summary>Batch-writes (puts) multiple items and returns them.</summary>
    /// <remarks>Batch writes bypass <c>[DynamoDBVersion]</c> optimistic concurrency, since DynamoDB's batch-write API has no conditional-write support; single-item <see cref="SaveAsync"/> still enforces it.</remarks>
    Task<DataOutput<IEnumerable<T>>> SaveManyAsync(IEnumerable<T> items, CancellationToken ct = default);

    /// <summary>Batch-deletes multiple items (idempotent).</summary>
    /// <remarks>Batch writes bypass <c>[DynamoDBVersion]</c> optimistic concurrency, since DynamoDB's batch-write API has no conditional-write support; single-item <see cref="DeleteAsync"/> still enforces it.</remarks>
    Task<ProcessOutput> DeleteManyAsync(IEnumerable<T> items, CancellationToken ct = default);

    /// <summary>Batch-gets items by partition key (hash-key-only tables).</summary>
    Task<DataOutput<IEnumerable<T>>> LoadManyAsync(IEnumerable<object> hashKeys, CancellationToken ct = default);
}
