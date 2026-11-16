using System.Runtime.CompilerServices;

namespace ArturRios.Data.Tests.MySql.TestSupport;

/// <summary>
///     A <see cref="FactAttribute"/> that skips itself unless a MySQL server is configured via
///     <see cref="MySqlTestServer.EnvironmentVariable"/>.
/// </summary>
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute([CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!MySqlTestServer.IsConfigured)
        {
            Skip = MySqlTestServer.SkipReason;
        }
    }
}
