using System.Linq.Expressions;
using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Interfaces;

/// <summary>Read-only document repository contract.</summary>
/// <typeparam name="T">The document type.</typeparam>
public interface IDocumentReadOnlyRepository<T> where T : Document
{
    /// <summary>Deferred, composable query over the collection.</summary>
    IQueryable<T> Query();

    /// <summary>Returns all documents.</summary>
    DataOutput<IEnumerable<T>> GetAll();

    /// <summary>Returns the document with the given id, or a successful null when none.</summary>
    DataOutput<T?> GetById(string id);

    /// <summary>Returns documents matching the predicate (server-side filter).</summary>
    DataOutput<IEnumerable<T>> Find(Expression<Func<T, bool>> predicate);
}
