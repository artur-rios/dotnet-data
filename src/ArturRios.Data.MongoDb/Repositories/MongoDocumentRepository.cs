using System.Linq.Expressions;
using ArturRios.Data.MongoDb.Exceptions;
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Output;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ArturRios.Data.MongoDb.Repositories;

/// <summary>
/// MongoDB implementation of the document repository contracts. Runs against the
/// <see cref="MongoContext"/> collection and enlists in its ambient session so operations
/// participate in a unit-of-work transaction. Failures are returned as <see cref="DataOutput{T}"/>.
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
/// <param name="context">The Mongo context.</param>
public class MongoDocumentRepository<T>(MongoContext context)
    : IDocumentRepository<T>, IAsyncDocumentRepository<T> where T : Document
{
    /// <summary>Message prefix returned when an operation fails.</summary>
    protected const string OperationFailedMessage = "A data-access error occurred:";

    /// <summary>Message returned on an optimistic-concurrency conflict.</summary>
    protected const string ConcurrencyMessage =
        "Concurrency conflict: the document was modified or removed by another process.";

    /// <summary>The collection for <typeparamref name="T"/>.</summary>
    protected IMongoCollection<T> Collection => context.GetCollection<T>();

    private IClientSessionHandle? Session => context.Session;

    /// <inheritdoc />
    public IQueryable<T> Query() => Collection.AsQueryable();

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> GetAll() =>
        Guarded(() => (IEnumerable<T>)FindFluent(FilterDefinition<T>.Empty).ToList());

    /// <inheritdoc />
    public DataOutput<T?> GetById(string id) =>
        Guarded<T?>(() => FindFluent(IdFilter(id)).FirstOrDefault());

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> Find(Expression<Func<T, bool>> predicate) =>
        Guarded(() => (IEnumerable<T>)FindFluent(Builders<T>.Filter.Where(predicate)).ToList());

    /// <inheritdoc />
    public DataOutput<string> Create(T document) => Guarded(() =>
    {
        EnsureId(document);
        InsertOne(document);
        return document.Id;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<string>> CreateRange(IEnumerable<T> documents) => Guarded(() =>
    {
        var list = documents.ToList();
        foreach (var d in list) EnsureId(d);
        InsertMany(list);
        return (IEnumerable<string>)list.Select(d => d.Id).ToList();
    });

    /// <inheritdoc />
    public DataOutput<T> Update(T document) => Guarded(() =>
    {
        Replace(document);
        return document;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> documents) => Guarded(() =>
    {
        var list = documents.ToList();
        foreach (var d in list) Replace(d);
        return (IEnumerable<T>)list;
    });

    /// <inheritdoc />
    public DataOutput<string> Delete(T document) => Guarded(() =>
    {
        DeleteMany(IdFilter(document.Id));
        return document.Id;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<string>> DeleteRange(IEnumerable<string> ids) => Guarded(() =>
    {
        var idList = ids.ToList();
        DeleteMany(Builders<T>.Filter.In(d => d.Id, idList));
        return (IEnumerable<string>)idList;
    });

    // Async members implemented in Task 5.
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<T?>> GetByIdAsync(string id, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<string>> CreateAsync(T document, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<string>>> CreateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<T>> UpdateAsync(T document, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<string>> DeleteAsync(T document, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<string>>> DeleteRangeAsync(IEnumerable<string> ids, CancellationToken ct = default) => throw new NotImplementedException();

    // --- session-aware driver helpers (sync) ---
    private static FilterDefinition<T> IdFilter(string id) => Builders<T>.Filter.Eq(d => d.Id, id);

    private static void EnsureId(T document)
    {
        if (string.IsNullOrEmpty(document.Id)) document.Id = ObjectId.GenerateNewId().ToString();
    }

    private IFindFluent<T, T> FindFluent(FilterDefinition<T> filter) =>
        Session is { } s ? Collection.Find(s, filter) : Collection.Find(filter);

    private void InsertOne(T document)
    {
        if (Session is { } s) Collection.InsertOne(s, document);
        else Collection.InsertOne(document);
    }

    private void InsertMany(IEnumerable<T> documents)
    {
        if (Session is { } s) Collection.InsertMany(s, documents);
        else Collection.InsertMany(documents);
    }

    private void DeleteMany(FilterDefinition<T> filter)
    {
        if (Session is { } s) Collection.DeleteMany(s, filter);
        else Collection.DeleteMany(filter);
    }

    // Replace with optimistic-concurrency handling for VersionedDocument.
    private void Replace(T document)
    {
        if (document is VersionedDocument versioned)
        {
            var expected = versioned.Version;
            versioned.Version = expected + 1;
            var filter = Builders<T>.Filter.And(IdFilter(document.Id),
                Builders<T>.Filter.Eq("Version", expected));
            var result = ReplaceOne(filter, document);
            if (result.MatchedCount == 0)
            {
                versioned.Version = expected; // roll back the in-memory bump on a failed (stale) update
                throw new MongoConcurrencyException();
            }
            return;
        }

        ReplaceOne(IdFilter(document.Id), document);
    }

    private ReplaceOneResult ReplaceOne(FilterDefinition<T> filter, T document) =>
        Session is { } s ? Collection.ReplaceOne(s, filter, document) : Collection.ReplaceOne(filter, document);

    /// <summary>Runs a synchronous operation, converting failures to envelope errors.</summary>
    protected static DataOutput<TResult> Guarded<TResult>(Func<TResult> operation)
    {
        try
        {
            return DataOutput<TResult>.New.WithData(operation());
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

    /// <summary>Maps an exception to an error envelope.</summary>
    protected static DataOutput<TResult> Fail<TResult>(Exception ex) => ex switch
    {
        MongoConcurrencyException => DataOutput<TResult>.New.WithError(ConcurrencyMessage),
        _ => DataOutput<TResult>.New.WithError($"{OperationFailedMessage} {ex.GetBaseException().Message}")
    };
}
