using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Interfaces;

/// <summary>Full read/write document repository contract.</summary>
/// <typeparam name="T">The document type.</typeparam>
public interface IDocumentRepository<T> : IDocumentReadOnlyRepository<T> where T : Document
{
    /// <summary>Inserts a document and returns its id.</summary>
    DataOutput<string> Create(T document);

    /// <summary>Inserts multiple documents and returns their ids.</summary>
    DataOutput<IEnumerable<string>> CreateRange(IEnumerable<T> documents);

    /// <summary>
    ///     Replaces an existing document. A document that no longer exists - or, for a
    ///     <see cref="VersionedDocument" />, whose stored version differs - is a concurrency-conflict error.
    /// </summary>
    DataOutput<T> Update(T document);

    /// <summary>
    ///     Replaces multiple existing documents, one at a time, stopping at the first that is missing or
    ///     stale (a concurrency-conflict error). Run it in a unit of work to make it all-or-nothing.
    /// </summary>
    DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> documents);

    /// <summary>
    ///     Deletes a document and returns its id. Deleting a missing plain document succeeds (idempotent);
    ///     a <see cref="VersionedDocument" /> is deleted only at the version held, and a missing or stale one
    ///     is a concurrency-conflict error.
    /// </summary>
    DataOutput<string> Delete(T document);

    /// <summary>
    ///     Deletes documents by id and returns the ids actually deleted; ids that match no document are
    ///     left out.
    /// </summary>
    DataOutput<IEnumerable<string>> DeleteRange(IEnumerable<string> ids);
}
