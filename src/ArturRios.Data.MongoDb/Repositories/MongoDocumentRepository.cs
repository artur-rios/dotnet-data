using System.Linq.Expressions;
using ArturRios.Data.MongoDb.Exceptions;
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Output;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
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

    // Cached: the serialized BSON element name for VersionedDocument.Version on T
    // (respects any element-name convention the consumer registered).
    private static readonly string VersionElementName =
        BsonClassMap.LookupClassMap(typeof(T)).AllMemberMaps
            .FirstOrDefault(m => m.MemberName == nameof(VersionedDocument.Version))?.ElementName
        ?? nameof(VersionedDocument.Version);

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

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () => await FindFluent(FilterDefinition<T>.Empty).ToListAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<T?>> GetByIdAsync(string id, CancellationToken ct = default) =>
        GuardedAsync<T?>(async () => await FindFluent(IdFilter(id)).FirstOrDefaultAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () => await FindFluent(Builders<T>.Filter.Where(predicate)).ToListAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<string>> CreateAsync(T document, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            EnsureId(document);
            await InsertOneAsync(document, ct);
            return document.Id;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<string>>> CreateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<string>>(async () =>
        {
            var list = documents.ToList();
            foreach (var d in list) EnsureId(d);
            await InsertManyAsync(list, ct);
            return list.Select(d => d.Id).ToList();
        });

    /// <inheritdoc />
    public Task<DataOutput<T>> UpdateAsync(T document, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            await ReplaceAsync(document, ct);
            return document;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () =>
        {
            var list = documents.ToList();
            foreach (var d in list) await ReplaceAsync(d, ct);
            return list;
        });

    /// <inheritdoc />
    public Task<DataOutput<string>> DeleteAsync(T document, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            await DeleteManyAsync(IdFilter(document.Id), ct);
            return document.Id;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<string>>> DeleteRangeAsync(IEnumerable<string> ids, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<string>>(async () =>
        {
            var idList = ids.ToList();
            await DeleteManyAsync(Builders<T>.Filter.In(d => d.Id, idList), ct);
            return idList;
        });

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
                Builders<T>.Filter.Eq(VersionElementName, expected));
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

    // --- session-aware driver helpers (async) ---
    private Task InsertOneAsync(T document, CancellationToken ct) =>
        Session is { } s ? Collection.InsertOneAsync(s, document, null, ct) : Collection.InsertOneAsync(document, null, ct);

    private Task InsertManyAsync(IEnumerable<T> documents, CancellationToken ct) =>
        Session is { } s ? Collection.InsertManyAsync(s, documents, null, ct) : Collection.InsertManyAsync(documents, null, ct);

    private Task DeleteManyAsync(FilterDefinition<T> filter, CancellationToken ct) =>
        Session is { } s ? Collection.DeleteManyAsync(s, filter, null, ct) : Collection.DeleteManyAsync(filter, ct);

    private async Task ReplaceAsync(T document, CancellationToken ct)
    {
        if (document is VersionedDocument versioned)
        {
            var expected = versioned.Version;
            versioned.Version = expected + 1;
            var filter = Builders<T>.Filter.And(IdFilter(document.Id), Builders<T>.Filter.Eq(VersionElementName, expected));
            var result = Session is { } s
                ? await Collection.ReplaceOneAsync(s, filter, document, cancellationToken: ct)
                : await Collection.ReplaceOneAsync(filter, document, cancellationToken: ct);
            if (result.MatchedCount == 0)
            {
                versioned.Version = expected; // roll back the in-memory bump on a failed (stale) update
                throw new MongoConcurrencyException();
            }
            return;
        }

        var idFilter = IdFilter(document.Id);
        if (Session is { } session)
            await Collection.ReplaceOneAsync(session, idFilter, document, cancellationToken: ct);
        else
            await Collection.ReplaceOneAsync(idFilter, document, cancellationToken: ct);
    }

    /// <summary>Runs an asynchronous operation, converting failures to envelope errors.</summary>
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

    /// <summary>Maps an exception to an error envelope.</summary>
    protected static DataOutput<TResult> Fail<TResult>(Exception ex) => ex switch
    {
        MongoConcurrencyException => DataOutput<TResult>.New.WithError(ConcurrencyMessage),
        _ => DataOutput<TResult>.New.WithError($"{OperationFailedMessage} {ex.GetBaseException().Message}")
    };
}
