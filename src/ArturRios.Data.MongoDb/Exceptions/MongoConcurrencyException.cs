using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Exceptions;

/// <summary>Raised internally when a versioned update matches no document (stale version).</summary>
public sealed class MongoConcurrencyException()
    : CustomException(["Concurrency conflict: the document was modified or removed by another process."]);
