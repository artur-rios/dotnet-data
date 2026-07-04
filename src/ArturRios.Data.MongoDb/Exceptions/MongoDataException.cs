using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Exceptions;

/// <summary>Internal typed exception for MongoDB data-access failures; converted to envelopes by repositories.</summary>
/// <param name="messages">The failure messages.</param>
public class MongoDataException(string[] messages) : CustomException(messages);
