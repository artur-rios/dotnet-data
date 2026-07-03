using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Configuration;

public class BaseDbContextTests
{
    [Fact]
    public void SaveChanges_RegeneratesConcurrencyStamp_OnModifiedVersionedEntity()
    {
        using var context = SqliteTestContextFactory.Create();
        var entity = new VersionedTestEntity { Name = "one" };
        context.VersionedItems.Add(entity);
        context.SaveChanges();
        var original = entity.ConcurrencyStamp;

        entity.Name = "two";
        context.SaveChanges();

        Assert.NotEqual(original, entity.ConcurrencyStamp);
    }

    [Fact]
    public void SaveChanges_DoesNotChangeStamp_WhenUnmodified()
    {
        using var context = SqliteTestContextFactory.Create();
        var entity = new VersionedTestEntity { Name = "one" };
        context.VersionedItems.Add(entity);
        context.SaveChanges();
        var stamp = entity.ConcurrencyStamp;

        context.SaveChanges(); // no changes

        Assert.Equal(stamp, entity.ConcurrencyStamp);
    }
}
