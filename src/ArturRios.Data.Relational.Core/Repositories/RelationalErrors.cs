using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Relational.Core.Repositories;

/// <summary>
///     Provider-independent classification of relational failures, and the caller-safe
///     messages that replace provider text. Provider messages name constraints, indexes,
///     columns, SQL fragments and conflicting values, so they are used for classification
///     only and are never returned to the caller. The full exception is still logged by
///     EF Core's own diagnostics, where operators can read it.
/// </summary>
public static class RelationalErrors
{
    /// <summary>Message returned when an optimistic-concurrency conflict is detected.</summary>
    public const string ConcurrencyMessage =
        "Concurrency conflict: the record was modified or removed by another process.";

    /// <summary>Message returned when a write violates a unique constraint.</summary>
    public const string UniqueViolationMessage = "Conflict: a record with the same unique value already exists.";

    /// <summary>Message returned when a write violates a non-unique integrity rule.</summary>
    public const string IntegrityViolationMessage = "Conflict: the operation violates a data-integrity rule.";

    /// <summary>Message returned when the failure is transient and the operation may be retried.</summary>
    public const string TransientMessage = "The data store is temporarily unavailable. Please retry.";

    /// <summary>Message returned when the failure has no caller-actionable classification.</summary>
    public const string GenericMessage = "A data-access error occurred.";

    // Markers for a unique/duplicate-key violation, for providers that report no SQLSTATE
    // or a class-level one (MySQL reports 23000 for every integrity violation).
    private static readonly string[] UniqueViolationMarkers =
    [
        "unique constraint", // PostgreSQL, Oracle, SQLite ("UNIQUE constraint failed: ...")
        "unique index", // SQL Server, SQLite
        "unique key", // SQL Server ("Violation of UNIQUE KEY constraint ...")
        "duplicate key", // SQL Server
        "duplicate entry" // MySQL / MariaDB
    ];

    // Markers for the remaining integrity violations: foreign key, not null, check.
    private static readonly string[] IntegrityViolationMarkers =
    [
        "constraint failed", // SQLite ("FOREIGN KEY constraint failed", "NOT NULL ...")
        "foreign key",
        "check constraint",
        "not-null",
        "cannot be null",
        "null value in column"
    ];

    /// <summary>
    ///     Detects a unique-constraint (duplicate-key) violation anywhere in the exception chain.
    ///     Recognised by SQLSTATE where the provider reports one, and by provider-neutral
    ///     duplicate-key wording otherwise.
    /// </summary>
    /// <param name="ex">The exception caught by a repository or query guard.</param>
    /// <returns><c>true</c> when the failure is a unique-constraint violation.</returns>
    public static bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is DbException db && IsUniqueViolation(db))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Classifies a failure and returns the caller-safe message for it. Never includes
    ///     provider text.
    /// </summary>
    /// <param name="ex">The exception caught by a repository or query guard.</param>
    /// <returns>One of the caller-safe messages exposed by this class.</returns>
    public static string Describe(Exception ex)
    {
        if (ex is DbUpdateConcurrencyException)
        {
            return ConcurrencyMessage;
        }

        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException)
            {
                return TransientMessage;
            }

            if (current is not DbException db)
            {
                continue;
            }

            if (IsUniqueViolation(db))
            {
                return UniqueViolationMessage;
            }

            if (IsIntegrityViolation(db))
            {
                return IntegrityViolationMessage;
            }

            if (db.IsTransient)
            {
                return TransientMessage;
            }
        }

        return GenericMessage;
    }

    private static bool IsUniqueViolation(DbException db) =>
        db.SqlState == "23505" || // PostgreSQL unique_violation
        Matches(db.Message, UniqueViolationMarkers);

    private static bool IsIntegrityViolation(DbException db) =>
        db.SqlState?.StartsWith("23", StringComparison.Ordinal) == true || // SQLSTATE integrity class
        Matches(db.Message, IntegrityViolationMarkers);

    private static bool Matches(string message, string[] markers) =>
        markers.Any(marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
