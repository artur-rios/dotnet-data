namespace ArturRios.Data.Tests.Export.TestSupport;

/// <summary>
///     A write-only stream that refuses synchronous writes, as ASP.NET Core's response body does by default
///     (<c>AllowSynchronousIO</c> is off), and keeps what is written asynchronously in <see cref="Written" />.
/// </summary>
public sealed class AsyncOnlyStream : Stream
{
    private const string Refused = "Synchronous operations are disallowed.";

    public MemoryStream Written { get; } = new();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new InvalidOperationException(Refused);
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException(Refused);
    public override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException(Refused);
    public override void WriteByte(byte value) => throw new InvalidOperationException(Refused);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Written.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        Written.WriteAsync(buffer, cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
