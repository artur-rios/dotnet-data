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

    /// <summary>Replaces an existing document.</summary>
    Task<DataOutput<T>> UpdateAsync(T document, CancellationToken ct = default);

    /// <summary>Replaces multiple existing documents.</summary>
    Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default);

    /// <summary>Deletes a document and returns its id.</summary>
    Task<DataOutput<string>> DeleteAsync(T document, CancellationToken ct = default);

    /// <summary>Deletes documents by id and returns the deleted ids.</summary>
    Task<DataOutput<IEnumerable<string>>> DeleteRangeAsync(IEnumerable<string> ids, CancellationToken ct = default);
}
