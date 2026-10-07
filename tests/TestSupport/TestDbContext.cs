using ArturRios.Data.Relational.Core.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.TestSupport;

public class TestDbContext(DbContextOptions options) : BaseDbContext(options)
{
    public DbSet<TestEntity> Items => Set<TestEntity>();
    public DbSet<VersionedTestEntity> VersionedItems => Set<VersionedTestEntity>();
    public DbSet<UniqueTestEntity> UniqueItems => Set<UniqueTestEntity>();
    public DbSet<GuidKeyedTestEntity> GuidKeyedItems => Set<GuidKeyedTestEntity>();
    public DbSet<StringKeyedTestEntity> StringKeyedItems => Set<StringKeyedTestEntity>();
    public DbSet<VersionedGuidKeyedTestEntity> VersionedGuidKeyedItems => Set<VersionedGuidKeyedTestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Explicit: the test project compiles with nullable reference types disabled, so EF
        // would map Email as a nullable column and NOT NULL violations could not be exercised.
        modelBuilder.Entity<UniqueTestEntity>()
            .Property(e => e.Email)
            .IsRequired();

        modelBuilder.Entity<UniqueTestEntity>()
            .HasIndex(e => e.Email)
            .IsUnique()
            .HasDatabaseName("IX_UniqueItems_Email");

        // Bounded so providers that cannot index unbounded text (MySQL) can use it as a primary key.
        modelBuilder.Entity<StringKeyedTestEntity>()
            .Property(e => e.Id)
            .HasMaxLength(64);
    }
}
