using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Interfaces;

/// <summary>Full asynchronous read/write document repository contract.</summary>
/// <typeparam name="T">The document type.</typeparam>
public interface IAsyncDocumentRepository<T> : IAsyncDocumentReadOnlyRepository<T> where T : Document
{
    /// <summary>Inserts a document and returns its id.</summary>
    Task<DataOutput<string>> CreateAsync(T document, CancellationToken ct = default);

    /// <summary>Inserts multiple documents and returns their ids.</summary>
    Task<DataOutput<IEnumerable<string>>> CreateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default);

    /// <summary>
    ///     Replaces an existing document. A document that no longer exists - or, for a
    ///     <see cref="VersionedDocument" />, whose stored version differs - is a concurrency-conflict error.
    /// </summary>
    Task<DataOutput<T>> UpdateAsync(T document, CancellationToken ct = default);

    /// <summary>
    ///     Replaces multiple existing documents, one at a time, stopping at the first that is missing or
    ///     stale (a concurrency-conflict error). Run it in a unit of work to make it all-or-nothing.
    /// </summary>
    Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default);

    /// <summary>
    ///     Deletes a document and returns its id. Deleting a missing plain document succeeds (idempotent);
    ///     a <see cref="VersionedDocument" /> is deleted only at the version held, and a missing or stale one
    ///     is a concurrency-conflict error.
    /// </summary>
    Task<DataOutput<string>> DeleteAsync(T document, CancellationToken ct = default);

    /// <summary>
    ///     Deletes documents by id and returns the ids actually deleted; ids that match no document are
    ///     left out.
    /// </summary>
    Task<DataOutput<IEnumerable<string>>> DeleteRangeAsync(IEnumerable<string> ids, CancellationToken ct = default);
}
