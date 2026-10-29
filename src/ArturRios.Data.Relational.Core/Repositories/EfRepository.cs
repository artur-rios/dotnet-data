using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Output;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Relational.Core.Repositories;

/// <summary>
///     Provider-agnostic Entity Framework Core implementation of the repository contracts.
///     Every write auto-saves; inside an active unit-of-work transaction,
///     saves flush without committing. Infrastructure failures are returned as <see cref="DataOutput{T}" /> errors;
///     a failed write discards the changes still pending in the context, so the context stays usable.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TKey">The entity's primary key type.</typeparam>
/// <param name="context">The application's <see cref="BaseDbContext" />.</param>
public class EfRepository<T, TKey>(BaseDbContext context) : IRepository<T, TKey>, IAsyncRepository<T, TKey>
    where T : Entity<TKey> where TKey : IEquatable<TKey>
{
    /// <summary>Message returned when an optimistic-concurrency conflict is detected.</summary>
    protected const string ConcurrencyMessage = RelationalErrors.ConcurrencyMessage;

    /// <summary>Message returned when a persistence operation fails with no finer classification.</summary>
    protected const string PersistenceMessage = RelationalErrors.GenericMessage;

    /// <summary>Message returned when a write violates a unique constraint.</summary>
    protected const string UniqueViolationMessage = RelationalErrors.UniqueViolationMessage;

    /// <summary>The tracked entity set for <typeparamref name="T" />.</summary>
    protected DbSet<T> Set => context.Set<T>();

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () => await Set.ToListAsync(ct).ConfigureAwait(false));

    /// <inheritdoc />
    public Task<DataOutput<T?>> GetByIdAsync(TKey id, CancellationToken ct = default) =>
        GuardedAsync(async () => await Set.FirstOrDefaultAsync(e => e.Id.Equals(id), ct).ConfigureAwait(false));

    /// <inheritdoc />
    public Task<DataOutput<TKey>> CreateAsync(T entity, CancellationToken ct = default) =>
        WriteAsync(async () =>
        {
            await Set.AddAsync(entity, ct).ConfigureAwait(false);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            return entity.Id;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<TKey>>>
        CreateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
        WriteAsync<IEnumerable<TKey>>(async () =>
        {
            var list = entities.ToList();
            await Set.AddRangeAsync(list, ct).ConfigureAwait(false);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            return list.Select(e => e.Id).ToList();
        });

    /// <inheritdoc />
    public Task<DataOutput<T>> UpdateAsync(T entity, CancellationToken ct = default) =>
        WriteAsync(async () =>
        {
            Set.Update(entity);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            return entity;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
        WriteAsync<IEnumerable<T>>(async () =>
        {
            var list = entities.ToList();
            Set.UpdateRange(list);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            return list;
        });

    /// <inheritdoc />
    public Task<DataOutput<TKey>> DeleteAsync(T entity, CancellationToken ct = default) =>
        WriteAsync(async () =>
        {
            Set.Remove(entity);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            return entity.Id;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<TKey>>> DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken ct = default) =>
        WriteAsync<IEnumerable<TKey>>(async () =>
        {
            var idList = ids.ToList();
            var matches = await Set.Where(e => idList.Contains(e.Id)).ToListAsync(ct).ConfigureAwait(false);
            Set.RemoveRange(matches);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            return matches.Select(e => e.Id).ToList();
        });

    /// <inheritdoc cref="Query" />
    public IQueryable<T> Query() => Set.AsQueryable();

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> GetAll() =>
        Guarded(IEnumerable<T> () => Set.ToList());

    /// <inheritdoc />
    public DataOutput<T?> GetById(TKey id) =>
        Guarded(() => Set.FirstOrDefault(e => e.Id.Equals(id)));

    /// <inheritdoc />
    public DataOutput<TKey> Create(T entity) => Write(() =>
    {
        Set.Add(entity);
        context.SaveChanges();
        return entity.Id;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<TKey>> CreateRange(IEnumerable<T> entities) => Write(IEnumerable<TKey> () =>
    {
        var list = entities.ToList();
        Set.AddRange(list);
        context.SaveChanges();
        return list.Select(e => e.Id).ToList();
    });

    /// <inheritdoc />
    public DataOutput<T> Update(T entity) => Write(() =>
    {
        Set.Update(entity);
        context.SaveChanges();
        return entity;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> entities) => Write(IEnumerable<T> () =>
    {
        var list = entities.ToList();
        Set.UpdateRange(list);
        context.SaveChanges();
        return list;
    });

    /// <inheritdoc />
    public DataOutput<TKey> Delete(T entity) => Write(() =>
    {
        Set.Remove(entity);
        context.SaveChanges();
        return entity.Id;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<TKey>> DeleteRange(IEnumerable<TKey> ids) => Write(IEnumerable<TKey> () =>
    {
        var idList = ids.ToList();
        var matches = Set.Where(e => idList.Contains(e.Id)).ToList();
        Set.RemoveRange(matches);
        context.SaveChanges();
        return matches.Select(e => e.Id).ToList();
    });

    /// <summary>
    ///     Maps an exception caught by a guard to an error envelope. Provider text - constraint
    ///     and index names, columns, SQL fragments, conflicting values - is classified but never
    ///     returned; EF Core logs the full exception for operators.
    /// </summary>
    private static DataOutput<TResult> Fail<TResult>(Exception ex) =>
        DataOutput<TResult>.New.WithError(RelationalErrors.Describe(ex));

    /// <summary>
    ///     Runs a write, converting failures to envelope errors. A failed save leaves its changes
    ///     pending in the change tracker, and the caller keeps using the same scoped context after an
    ///     error envelope, so the pending changes are discarded: otherwise the next save on the context
    ///     would replay the rejected change and fail again.
    /// </summary>
    private DataOutput<TResult> Write<TResult>(Func<TResult> operation)
    {
        try
        {
            var output = Guarded(operation);
            if (!output.Success)
            {
                DiscardPendingChanges();
            }

            return output;
        }
        catch (OperationCanceledException)
        {
            DiscardPendingChanges();
            throw;
        }
    }

    /// <inheritdoc cref="Write{TResult}" />
    private async Task<DataOutput<TResult>> WriteAsync<TResult>(Func<Task<TResult>> operation)
    {
        try
        {
            var output = await GuardedAsync(operation).ConfigureAwait(false);
            if (!output.Success)
            {
                DiscardPendingChanges();
            }

            return output;
        }
        catch (OperationCanceledException)
        {
            DiscardPendingChanges();
            throw;
        }
    }

    private void DiscardPendingChanges()
    {
        foreach (var entry in context.ChangeTracker.Entries()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>Runs a synchronous data operation, converting failures to envelope errors.</summary>
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

    /// <summary>Runs an asynchronous data operation, converting failures to envelope errors.</summary>
    protected static async Task<DataOutput<TResult>> GuardedAsync<TResult>(Func<Task<TResult>> operation)
    {
        try
        {
            return DataOutput<TResult>.New.WithData(await operation().ConfigureAwait(false));
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
}
