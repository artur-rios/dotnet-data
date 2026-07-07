using ArturRios.Data.Relational.Core.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.TestSupport;

public class TestDbContext(DbContextOptions options) : BaseDbContext(options)
{
    public DbSet<TestEntity> Items => Set<TestEntity>();
    public DbSet<VersionedTestEntity> VersionedItems => Set<VersionedTestEntity>();
}
