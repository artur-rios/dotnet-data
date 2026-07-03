using ArturRios.Output;

namespace ArturRios.Data.Dapper;

/// <summary>
/// Read-only raw-SQL query surface (asynchronous). Backed by Dapper over the application's
/// database connection. All results are enveloped in <see cref="DataOutput{T}"/>.
/// </summary>
public interface IAsyncSqlQuery
{
    /// <summary>Executes a query and maps every row to <typeparamref name="T"/>.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <typeparam name="T">The row type to map to.</typeparam>
    Task<DataOutput<IEnumerable<T>>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Returns the first row mapped to <typeparamref name="T"/>, or a successful null when none.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <typeparam name="T">The row type to map to.</typeparam>
    Task<DataOutput<T?>> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Returns the single row mapped to <typeparamref name="T"/>, or a successful null when none.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <typeparam name="T">The row type to map to.</typeparam>
    Task<DataOutput<T?>> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Executes a query and returns the first column of the first row.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    Task<DataOutput<T?>> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);
}
