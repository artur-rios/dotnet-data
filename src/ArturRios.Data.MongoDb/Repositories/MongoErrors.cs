using ArturRios.Data.MongoDb.Exceptions;
using MongoDB.Driver;

namespace ArturRios.Data.MongoDb.Repositories;

/// <summary>
///     Classification of MongoDB failures, and the caller-safe messages that replace driver text.
///     Driver messages name indexes, collections, key values and cluster endpoints, so they are used
///     for classification only and are never returned to the caller.
/// </summary>
public static class MongoErrors
{
    /// <summary>Message returned on an optimistic-concurrency conflict.</summary>
    public const string ConcurrencyMessage =
        "Concurrency conflict: the document was modified or removed by another process.";

    /// <summary>Message returned when a write violates a unique index.</summary>
    public const string UniqueViolationMessage = "Conflict: a document with the same unique value already exists.";

    /// <summary>Message returned when the failure is transient and the operation may be retried.</summary>
    public const string TransientMessage = "The data store is temporarily unavailable. Please retry.";

    /// <summary>Message returned when an operation fails with no finer classification.</summary>
    public const string GenericMessage = "A data-access error occurred.";

    // Server error codes for a duplicate key on a unique index.
    private const int DuplicateKeyCode = 11000;
    private const int DuplicateKeyOnUpdateCode = 11001;

    /// <summary>Detects a duplicate-key (unique index) violation anywhere in the exception chain.</summary>
    /// <param name="ex">The exception caught by a guard.</param>
    /// <returns><c>true</c> when the failure is a duplicate-key violation.</returns>
    public static bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var duplicate = current switch
            {
                MongoWriteException write => write.WriteError?.Category == ServerErrorCategory.DuplicateKey,
                MongoBulkWriteException bulk => bulk.WriteErrors.Any(e =>
                    e.Category == ServerErrorCategory.DuplicateKey),
                MongoCommandException command => command.Code is DuplicateKeyCode or DuplicateKeyOnUpdateCode,
                _ => false
            };

            if (duplicate)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Detects a failure the caller may retry: connection, election or timeout.</summary>
    /// <param name="ex">The exception caught by a guard.</param>
    /// <returns><c>true</c> when the failure is transient.</returns>
    public static bool IsTransient(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is MongoConnectionException or MongoNodeIsRecoveringException
                or MongoNotPrimaryException or MongoExecutionTimeoutException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Classifies a failure and returns the caller-safe message for it. Never includes driver text.
    /// </summary>
    /// <param name="ex">The exception caught by a guard.</param>
    /// <returns>One of the caller-safe messages exposed by this class.</returns>
    public static string Describe(Exception ex) => ex switch
    {
        MongoConcurrencyException => ConcurrencyMessage,
        _ when IsUniqueViolation(ex) => UniqueViolationMessage,
        _ when IsTransient(ex) => TransientMessage,
        _ => GenericMessage
    };
}
