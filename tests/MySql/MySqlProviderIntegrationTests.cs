using ArturRios.Data.MySql;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Data.Relational.Core.Transactions;
using ArturRios.Data.Tests.MySql.TestSupport;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microting.EntityFrameworkCore.MySql.Infrastructure.Internal;

namespace ArturRios.Data.Tests.MySql;

/// <summary>
///     End-to-end checks that <see cref="ArturRios.Data.MySql.MySqlProvider"/> configures a context that
///     actually talks to a MySQL server. Skipped unless
///     <see cref="MySqlTestServer.EnvironmentVariable"/> is set.
/// </summary>
[Collection(MySqlTestCollection.Name)]
[Trait("Category", "Integration")]
public class MySqlProviderIntegrationTests(MySqlDatabaseFixture fixture)
{
    [MySqlFact]
    public void GivenAContextBuiltByTheProvider_WhenInspected_ThenItUsesTheMySqlProvider()
    {
        using var context = fixture.CreateContext();

        Assert.Equal("Microting.EntityFrameworkCore.MySql", context.Database.ProviderName);
        Assert.True(context.Database.CanConnect());
    }

    [MySqlFact]
    public void GivenTheSameConnectionString_WhenConfiguredTwice_ThenTheServerVersionIsDetectedOnceAndReused()
    {
        var provider = new MySqlProvider();
        var connectionString = MySqlTestServer.ConnectionStringFor(fixture.Database);
        var first = new DbContextOptionsBuilder<TestDbContext>();
        var second = new DbContextOptionsBuilder<TestDbContext>();

        provider.Configure(first, connectionString);
        provider.Configure(second, connectionString);

        // The options extension is the only place the configured server version is exposed.
#pragma warning disable EF1001
        var firstVersion = first.Options.FindExtension<MySqlOptionsExtension>()!.ServerVersion;
        var secondVersion = second.Options.FindExtension<MySqlOptionsExtension>()!.ServerVersion;
#pragma warning restore EF1001
        Assert.Same(firstVersion, secondVersion);
    }

    [MySqlFact]
    public void GivenANewEntity_WhenCreatedAndFetchedBack_ThenItRoundTripsThroughMySql()
    {
        fixture.Reset();
        using var context = fixture.CreateContext();
        var repo = new EfRepository<TestEntity, long>(context);

        var created = repo.Create(new TestEntity { Name = "round-trip" });
        var fetched = repo.GetById(created.Data);

        Assert.True(created.Success);
        Assert.True(created.Data > 0);
        Assert.True(fetched.Success);
        Assert.Equal("round-trip", fetched.Data!.Name);
    }

    [MySqlFact]
    public void GivenAnEntityWrittenInOneContext_WhenReadInAnother_ThenMySqlPersistedIt()
    {
        fixture.Reset();

        long id;

        using (var writer = fixture.CreateContext())
        {
            id = new EfRepository<TestEntity, long>(writer).Create(new TestEntity { Name = "durable" }).Data;
        }

        using var reader = fixture.CreateContext();
        var result = new EfRepository<TestEntity, long>(reader).GetById(id);

        Assert.True(result.Success);
        Assert.Equal("durable", result.Data!.Name);
    }

    [MySqlFact]
    public void GivenWorkThatThrows_WhenExecutedInAMySqlTransaction_ThenItRollsBack()
    {
        fixture.Reset();
        using var context = fixture.CreateContext();
        var repo = new EfRepository<TestEntity, long>(context);

        var result = new EfUnitOfWork(context).ExecuteInTransaction(() =>
        {
            repo.Create(new TestEntity { Name = "rolled-back" });
            throw new InvalidOperationException("boom");
        });

        Assert.False(result.Success);
        Assert.Empty(repo.GetAll().Data!);
    }

    [MySqlFact]
    public void GivenAUniqueIndexViolation_WhenInserting_ThenAnErrorEnvelopeComesBackWithoutProviderText()
    {
        fixture.Reset();
        using var context = fixture.CreateContext();
        var repo = new EfRepository<UniqueTestEntity, long>(context);

        repo.Create(new UniqueTestEntity { Email = "duplicate@example.com" });
        var result = repo.Create(new UniqueTestEntity { Email = "duplicate@example.com" });

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.DoesNotContain(result.Errors, error => error.Contains("IX_UniqueItems_Email", StringComparison.OrdinalIgnoreCase));
    }

    [MySqlFact]
    public void GivenAStaleVersionedEntity_WhenUpdated_ThenMySqlOptimisticConcurrencyIsDetected()
    {
        fixture.Reset();

        long id;

        using (var seed = fixture.CreateContext())
        {
            id = new EfRepository<VersionedTestEntity, long>(seed).Create(new VersionedTestEntity { Name = "v1" }).Data;
        }

        using var first = fixture.CreateContext();
        using var second = fixture.CreateContext();

        var stale = first.Set<VersionedTestEntity>().Single(e => e.Id == id);
        var winner = new EfRepository<VersionedTestEntity, long>(second).GetById(id).Data!;

        winner.Name = "winner";
        new EfRepository<VersionedTestEntity, long>(second).Update(winner);

        stale.Name = "loser";
        var result = new EfRepository<VersionedTestEntity, long>(first).Update(stale);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }
}
