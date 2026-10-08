using ArturRios.Data.Relational.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Relational.Core.Configuration;

/// <summary>
///     Base <see cref="DbContext" /> that applies shared conventions and refreshes the
///     optimistic-concurrency stamp of modified <see cref="IVersionedEntity" /> instances on save.
/// </summary>
/// <param name="options">The context options supplied by the configured provider.</param>
public abstract class BaseDbContext(DbContextOptions options) : DbContext(options)
{
    // The parameterless SaveChanges / SaveChangesAsync overloads delegate to these two, so overriding
    // them covers every save entry point.

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var bumped = BumpConcurrencyStamps();

        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch
        {
            RestoreConcurrencyStamps(bumped);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var bumped = BumpConcurrencyStamps();

        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            RestoreConcurrencyStamps(bumped);
            throw;
        }
    }

    private List<(IVersionedEntity Entity, Guid Stamp)> BumpConcurrencyStamps()
    {
        var bumped = new List<(IVersionedEntity Entity, Guid Stamp)>();

        foreach (var entry in ChangeTracker.Entries<IVersionedEntity>()
                     .Where(e => e.State == EntityState.Modified))
        {
            bumped.Add((entry.Entity, entry.Entity.ConcurrencyStamp));
            entry.Entity.ConcurrencyStamp = Guid.NewGuid();
        }

        return bumped;
    }

    // The save never landed, so the in-memory bump must not survive: keeping it would leave the
    // entity carrying a stamp the database never stored, and every retry would then fail as a
    // concurrency conflict.
    private static void RestoreConcurrencyStamps(List<(IVersionedEntity Entity, Guid Stamp)> bumped)
    {
        foreach (var (entity, stamp) in bumped)
        {
            entity.ConcurrencyStamp = stamp;
        }
    }
}
