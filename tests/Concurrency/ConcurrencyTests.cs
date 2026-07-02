using ArturRios.Data.Repositories;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.Concurrency;

public class ConcurrencyTests
{
    [Fact]
    public void Update_WithStaleStamp_ReturnsConcurrencyError()
    {
        // Two contexts over the SAME in-memory database via a shared connection.
        using var writer = SqliteTestContextFactory.Create();
        var repo = new EfRepository<VersionedTestEntity>(writer);

        var entity = new VersionedTestEntity { Name = "original" };
        repo.Create(entity);

        // Capture the stamp issued at creation time, then detach 'entity' from the
        // context. EF tracks entities by key, so without detaching, the later
        // Set.Update(entity) call would just re-attach onto the already-tracked
        // 'fresh' instance and never see a stale value — defeating the simulation.
        var staleStamp = entity.ConcurrencyStamp;
        writer.Entry(entity).State = EntityState.Detached;

        // Load a second tracked copy, mutate & save it (advancing the stored stamp).
        var fresh = repo.GetById(entity.Id).Data!;
        fresh.Name = "updated-by-other";
        var firstUpdate = repo.Update(fresh);
        Assert.True(firstUpdate.Success);

        // Detach 'fresh' too, so re-attaching 'entity' under the same key doesn't
        // collide with an already-tracked instance in the identity map.
        writer.Entry(fresh).State = EntityState.Detached;

        // The first 'entity' instance still holds the old ConcurrencyStamp -> stale.
        entity.ConcurrencyStamp = staleStamp;
        entity.Name = "late-write";
        var staleUpdate = repo.Update(entity);

        Assert.False(staleUpdate.Success);
        Assert.Contains(staleUpdate.Errors, e => e.Contains("Concurrency conflict"));
    }
}
