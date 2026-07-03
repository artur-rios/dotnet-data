using System.Data.Common;
using ArturRios.Data.Core.Configuration;
using ArturRios.Output;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArturRios.Data.Dapper;

/// <summary>
/// Dapper-backed read-only query executor. Runs against the <see cref="BaseDbContext"/>'s
/// connection and enlists in its ambient transaction, so Dapper reads and EF writes share one
/// connection and one unit-of-work transaction. Failures are returned as <see cref="DataOutput{T}"/>.
/// </summary>
/// <param name="context">The application's <see cref="BaseDbContext"/>.</param>
public class DapperSqlQuery(BaseDbContext context) : ISqlQuery, IAsyncSqlQuery
{
    /// <summary>Message prefix returned when a query fails.</summary>
    protected const string QueryFailedMessage = "A data-access error occurred:";

    /// <summary>The context's underlying database connection.</summary>
    protected DbConnection Connection => context.Database.GetDbConnection();

    /// <summary>The ambient database transaction, or <see langword="null"/> when none is active.</summary>
    protected DbTransaction? Transaction => context.Database.CurrentTransaction?.GetDbTransaction();

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> Query<T>(string sql, object? parameters = null) =>
        Guarded(() => Connection.Query<T>(sql, parameters, Transaction));

    /// <inheritdoc />
    public DataOutput<T?> QueryFirstOrDefault<T>(string sql, object? parameters = null) =>
        Guarded(() => Connection.QueryFirstOrDefault<T?>(sql, parameters, Transaction));

    /// <inheritdoc />
    public DataOutput<T?> QuerySingleOrDefault<T>(string sql, object? parameters = null) =>
        Guarded(() => Connection.QuerySingleOrDefault<T?>(sql, parameters, Transaction));

    /// <inheritdoc />
    public DataOutput<T?> ExecuteScalar<T>(string sql, object? parameters = null) =>
        Guarded(() => Connection.ExecuteScalar<T?>(sql, parameters, Transaction));

    // Async members implemented in Task 3.
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<T?>> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<T?>> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<T?>> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) => throw new NotImplementedException();

    /// <summary>Runs a synchronous query, converting failures to envelope errors.</summary>
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
            return DataOutput<TResult>.New.WithError($"{QueryFailedMessage} {ex.GetBaseException().Message}");
        }
    }
}
