using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Configuration;

/// <summary>
/// Base <see cref="DbContext"/> that applies shared conventions and refreshes the
/// optimistic-concurrency stamp of modified <see cref="VersionedEntity"/> instances on save.
/// </summary>
/// <param name="options">The context options supplied by the configured provider.</param>
public abstract class BaseDbContext(DbContextOptions options) : DbContext(options)
{
    /// <inheritdoc />
    public override int SaveChanges()
    {
        BumpConcurrencyStamps();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        BumpConcurrencyStamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void BumpConcurrencyStamps()
    {
        foreach (var entry in ChangeTracker.Entries<VersionedEntity>()
                     .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.ConcurrencyStamp = Guid.NewGuid();
        }
    }
}
