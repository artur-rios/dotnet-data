using System.Linq.Expressions;
using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Interfaces;

/// <summary>Asynchronous read-only document repository contract.</summary>
/// <typeparam name="T">The document type.</typeparam>
public interface IAsyncDocumentReadOnlyRepository<T> where T : Document
{
    /// <summary>Deferred, composable query over the collection.</summary>
    /// <remarks>
    /// Runs OUTSIDE any ambient unit-of-work transaction (the driver's LINQ provider does not use the
    /// session), so it will not see uncommitted writes made earlier in the same transaction — use
    /// <see cref="GetAllAsync"/>/<see cref="FindAsync"/> for transaction-aware reads.
    /// </remarks>
    IQueryable<T> Query();

    /// <summary>Returns all documents.</summary>
    Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns the document with the given id, or a successful null when none.</summary>
    Task<DataOutput<T?>> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>Returns documents matching the predicate (server-side filter).</summary>
    Task<DataOutput<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}
