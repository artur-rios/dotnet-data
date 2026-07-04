using ArturRios.Output;

namespace ArturRios.Data.Dapper;

/// <summary>
/// Read-only raw-SQL query surface (synchronous). Backed by Dapper over the application's
/// database connection. All results are enveloped in <see cref="DataOutput{T}"/>.
/// </summary>
public interface ISqlQuery
{
    /// <summary>Executes a query and maps every row to <typeparamref name="T"/>.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <typeparam name="T">The row type to map to.</typeparam>
    DataOutput<IEnumerable<T>> Query<T>(string sql, object? parameters = null);

    /// <summary>Returns the first row mapped to <typeparamref name="T"/>, or a successful null when none.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <typeparam name="T">The row type to map to.</typeparam>
    DataOutput<T?> QueryFirstOrDefault<T>(string sql, object? parameters = null);

    /// <summary>Returns the single row mapped to <typeparamref name="T"/>, or a successful null when none.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <typeparam name="T">The row type to map to.</typeparam>
    DataOutput<T?> QuerySingleOrDefault<T>(string sql, object? parameters = null);

    /// <summary>Executes a query and returns the first column of the first row.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    DataOutput<T?> ExecuteScalar<T>(string sql, object? parameters = null);
}
