using System.Data.Common;
using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Output;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Relational.Core.Repositories;

/// <summary>
///     Provider-agnostic Entity Framework Core implementation of the repository contracts.
///     Every write auto-saves; inside an active unit-of-work transaction,
///     saves flush without committing. Infrastructure failures are returned as <see cref="DataOutput{T}" /> errors.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <param name="context">The application's <see cref="BaseDbContext" />.</param>
public class EfRepository<T>(BaseDbContext context) : IRepository<T>, IAsyncRepository<T>
    where T : Entity
{
    /// <summary>Message returned when an optimistic-concurrency conflict is detected.</summary>
    protected const string ConcurrencyMessage =
        "Concurrency conflict: the record was modified or removed by another process.";

    /// <summary>Message prefix returned when a persistence operation fails.</summary>
    protected const string PersistenceMessage = "A data-access error occurred:";

    /// <summary>The tracked entity set for <typeparamref name="T" />.</summary>
    protected DbSet<T> Set => context.Set<T>();

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () => await Set.ToListAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<T?>> GetByIdAsync(int id, CancellationToken ct = default) =>
        GuardedAsync(async () => await Set.FirstOrDefaultAsync(e => e.Id == id, ct));

    /// <inheritdoc />
    public Task<DataOutput<int>> CreateAsync(T entity, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            await Set.AddAsync(entity, ct);
            await context.SaveChangesAsync(ct);
            return entity.Id;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<int>>>
        CreateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<int>>(async () =>
        {
            var list = entities.ToList();
            await Set.AddRangeAsync(list, ct);
            await context.SaveChangesAsync(ct);
            return list.Select(e => e.Id).ToList();
        });

    /// <inheritdoc />
    public Task<DataOutput<T>> UpdateAsync(T entity, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            Set.Update(entity);
            await context.SaveChangesAsync(ct);
            return entity;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () =>
        {
            var list = entities.ToList();
            Set.UpdateRange(list);
            await context.SaveChangesAsync(ct);
            return list;
        });

    /// <inheritdoc />
    public Task<DataOutput<int>> DeleteAsync(T entity, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            Set.Remove(entity);
            await context.SaveChangesAsync(ct);
            return entity.Id;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<int>>> DeleteRangeAsync(IEnumerable<int> ids, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<int>>(async () =>
        {
            var idList = ids.ToList();
            var matches = await Set.Where(e => idList.Contains(e.Id)).ToListAsync(ct);
            Set.RemoveRange(matches);
            await context.SaveChangesAsync(ct);
            return matches.Select(e => e.Id).ToList();
        });

    /// <inheritdoc />
    public IQueryable<T> Query() => Set.AsQueryable();

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> GetAll() =>
        Guarded(() => (IEnumerable<T>)Set.ToList());

    /// <inheritdoc />
    public DataOutput<T?> GetById(int id) =>
        Guarded(() => Set.FirstOrDefault(e => e.Id == id));

    /// <inheritdoc />
    public DataOutput<int> Create(T entity) => Guarded(() =>
    {
        Set.Add(entity);
        context.SaveChanges();
        return entity.Id;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<int>> CreateRange(IEnumerable<T> entities) => Guarded(() =>
    {
        var list = entities.ToList();
        Set.AddRange(list);
        context.SaveChanges();
        return (IEnumerable<int>)list.Select(e => e.Id).ToList();
    });

    /// <inheritdoc />
    public DataOutput<T> Update(T entity) => Guarded(() =>
    {
        Set.Update(entity);
        context.SaveChanges();
        return entity;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> entities) => Guarded(() =>
    {
        var list = entities.ToList();
        Set.UpdateRange(list);
        context.SaveChanges();
        return (IEnumerable<T>)list;
    });

    /// <inheritdoc />
    public DataOutput<int> Delete(T entity) => Guarded(() =>
    {
        Set.Remove(entity);
        context.SaveChanges();
        return entity.Id;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<int>> DeleteRange(IEnumerable<int> ids) => Guarded(() =>
    {
        var idList = ids.ToList();
        var matches = Set.Where(e => idList.Contains(e.Id)).ToList();
        Set.RemoveRange(matches);
        context.SaveChanges();
        return (IEnumerable<int>)matches.Select(e => e.Id).ToList();
    });

    /// <summary>Maps an exception caught by a guard to an error envelope.</summary>
    private static DataOutput<TResult> Fail<TResult>(Exception ex) => ex switch
    {
        DbUpdateConcurrencyException => DataOutput<TResult>.New.WithError(ConcurrencyMessage),
        DbUpdateException => DataOutput<TResult>.New.WithError($"{PersistenceMessage} {ex.GetBaseException().Message}"),
        DbException => DataOutput<TResult>.New.WithError($"{PersistenceMessage} {ex.Message}"),
        _ => DataOutput<TResult>.New.WithError($"{PersistenceMessage} {ex.GetBaseException().Message}")
    };

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
}
