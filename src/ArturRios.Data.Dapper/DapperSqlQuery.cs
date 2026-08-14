using System.Data.Common;
using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Output;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ArturRios.Data.Dapper;

/// <summary>
///     Dapper-backed read-only query executor. Runs against the <see cref="BaseDbContext" />'s
///     connection and enlists in its ambient transaction, so Dapper reads and EF writes share one
///     connection and one unit-of-work transaction. Failures are returned as <see cref="DataOutput{T}" />.
/// </summary>
/// <param name="context">The application's <see cref="BaseDbContext" />.</param>
/// <param name="logger">
///     Optional logger. Envelopes never carry provider text, so a query failure is otherwise
///     undiagnosable: supply a logger and the full exception, plus the SQL that produced it, is
///     written at <see cref="LogLevel.Error" />. Parameter values are never logged - they are the
///     part most likely to hold personal data. Resolved from DI when logging is registered.
/// </param>
public class DapperSqlQuery(BaseDbContext context, ILogger<DapperSqlQuery>? logger = null)
    : ISqlQuery, IAsyncSqlQuery
{
    /// <summary>Message returned when a query fails with no finer classification.</summary>
    protected const string QueryFailedMessage = RelationalErrors.GenericMessage;

    /// <summary>The context's underlying database connection.</summary>
    protected DbConnection Connection => context.Database.GetDbConnection();

    /// <summary>The ambient database transaction, or <see langword="null" /> when none is active.</summary>
    protected DbTransaction? Transaction => context.Database.CurrentTransaction?.GetDbTransaction();

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> QueryAsync<T>(string sql, object? parameters = null,
        CancellationToken ct = default) =>
        GuardedAsync(sql, async () => await Connection.QueryAsync<T>(Command(sql, parameters, ct)));

    /// <inheritdoc />
    public Task<DataOutput<T?>> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null,
        CancellationToken ct = default) =>
        GuardedAsync(sql, async () => await Connection.QueryFirstOrDefaultAsync<T?>(Command(sql, parameters, ct)));

    /// <inheritdoc />
    public Task<DataOutput<T?>> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null,
        CancellationToken ct = default) =>
        GuardedAsync(sql, async () => await Connection.QuerySingleOrDefaultAsync<T?>(Command(sql, parameters, ct)));

    /// <inheritdoc />
    public Task<DataOutput<T?>> ExecuteScalarAsync<T>(string sql, object? parameters = null,
        CancellationToken ct = default) =>
        GuardedAsync(sql, async () => await Connection.ExecuteScalarAsync<T?>(Command(sql, parameters, ct)));

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> Query<T>(string sql, object? parameters = null) =>
        Guarded(sql, () => Connection.Query<T>(sql, parameters, Transaction));

    /// <inheritdoc />
    public DataOutput<T?> QueryFirstOrDefault<T>(string sql, object? parameters = null) =>
        Guarded(sql, () => Connection.QueryFirstOrDefault<T?>(sql, parameters, Transaction));

    /// <inheritdoc />
    public DataOutput<T?> QuerySingleOrDefault<T>(string sql, object? parameters = null) =>
        Guarded(sql, () => Connection.QuerySingleOrDefault<T?>(sql, parameters, Transaction));

    /// <inheritdoc />
    public DataOutput<T?> ExecuteScalar<T>(string sql, object? parameters = null) =>
        Guarded(sql, () => Connection.ExecuteScalar<T?>(sql, parameters, Transaction));

    /// <summary>Runs a synchronous query, converting failures to envelope errors.</summary>
    /// <param name="sql">The SQL being run, used as log context when a logger is configured.</param>
    /// <param name="operation">The query to run.</param>
    protected DataOutput<TResult> Guarded<TResult>(string sql, Func<TResult> operation)
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
            return Fail<TResult>(ex, sql);
        }
    }

    /// <summary>Builds a Dapper command carrying the ambient transaction and cancellation token.</summary>
    private CommandDefinition Command(string sql, object? parameters, CancellationToken ct) =>
        new(sql, parameters, Transaction, cancellationToken: ct);

    /// <summary>Runs an asynchronous query, converting failures to envelope errors.</summary>
    /// <param name="sql">The SQL being run, used as log context when a logger is configured.</param>
    /// <param name="operation">The query to run.</param>
    protected async Task<DataOutput<TResult>> GuardedAsync<TResult>(string sql, Func<Task<TResult>> operation)
    {
        try
        {
            return DataOutput<TResult>.New.WithData(await operation());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail<TResult>(ex, sql);
        }
    }

    /// <summary>
    ///     Logs the failure in full when a logger is configured, and returns the caller-safe
    ///     envelope. Provider text names constraints, columns and SQL fragments, so it goes to
    ///     the log and never to the caller.
    /// </summary>
    private DataOutput<TResult> Fail<TResult>(Exception ex, string sql)
    {
        logger?.LogError(ex, "Dapper query failed. SQL: {Sql}", sql);

        return DataOutput<TResult>.New.WithError(RelationalErrors.Describe(ex));
    }
}
