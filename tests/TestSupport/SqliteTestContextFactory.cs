using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.TestSupport;

/// <summary>
///     Builds a TestDbContext backed by a real SQLite in-memory database. The returned
///     context owns an open connection; dispose the context to close it.
/// </summary>
public static class SqliteTestContextFactory
{
    public static TestDbContext Create()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
