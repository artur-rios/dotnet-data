using ArturRios.Output;

namespace ArturRios.Data.DynamoDb.Exceptions;

/// <summary>Internal typed exception for DynamoDB data-access failures; converted to envelopes by the repository.</summary>
/// <param name="messages">The failure messages.</param>
public class DynamoDataException(string[] messages) : CustomException(messages);
