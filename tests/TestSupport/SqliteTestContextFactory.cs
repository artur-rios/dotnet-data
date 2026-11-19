using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArturRios.Data.Tests.TestSupport;

/// <summary>
///     Builds a TestDbContext backed by a real SQLite in-memory database. The returned
///     context owns an open connection; dispose the context to close it.
/// </summary>
public static class SqliteTestContextFactory
{
    /// <param name="interceptors">EF Core interceptors to add, e.g. to make a save fail a given way.</param>
    public static TestDbContext Create(params IInterceptor[] interceptors)
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptors)
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
