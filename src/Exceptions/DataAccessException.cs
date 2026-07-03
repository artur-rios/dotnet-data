using ArturRios.Output;

namespace ArturRios.Data.Core.Exceptions;

/// <summary>
/// Internal typed exception for data-access failures. Repositories catch this (and
/// underlying provider exceptions) and convert them to <see cref="DataOutput{T}"/> errors;
/// it is not intended to propagate out of a repository method.
/// </summary>
/// <param name="messages">The failure messages.</param>
public class DataAccessException(string[] messages) : CustomException(messages);
