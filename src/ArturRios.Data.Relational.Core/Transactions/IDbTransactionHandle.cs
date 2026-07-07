namespace ArturRios.Data.Relational.Core.Transactions;

/// <summary>
/// A handle over an active database transaction with manual commit/rollback control.
/// </summary>
public interface IDbTransactionHandle : IDisposable, IAsyncDisposable
{
    /// <summary>Commits the transaction.</summary>
    void Commit();

    /// <summary>Rolls the transaction back.</summary>
    void Rollback();

    /// <summary>Commits the transaction asynchronously.</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Rolls the transaction back asynchronously.</summary>
    Task RollbackAsync(CancellationToken ct = default);
}
