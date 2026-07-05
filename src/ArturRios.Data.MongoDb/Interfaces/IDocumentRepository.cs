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

    /// <summary>Replaces an existing document.</summary>
    DataOutput<T> Update(T document);

    /// <summary>Replaces multiple existing documents.</summary>
    DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> documents);

    /// <summary>Deletes a document and returns its id.</summary>
    DataOutput<string> Delete(T document);

    /// <summary>Deletes documents by id and returns the deleted ids.</summary>
    DataOutput<IEnumerable<string>> DeleteRange(IEnumerable<string> ids);
}
