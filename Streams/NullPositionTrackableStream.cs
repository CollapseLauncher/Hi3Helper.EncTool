using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Hi3Helper.EncTool.Streams;

/// <summary>Provides a nop stream but with position tracking while reading or writing files.</summary>
public sealed class NullPositionTrackableStream : Stream
{
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => true;
    public override long Length => 0;

    public override long Position
    {
        get;
        set;
    }

    public override void CopyTo(Stream destination, int bufferSize) { }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ?
            Task.FromCanceled(cancellationToken) :
            Task.CompletedTask;

    protected override void Dispose(bool disposing)
    {
        // Do nothing - we don't want NullStream singleton (static) to be closable
    }

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ?
            Task.FromCanceled(cancellationToken) :
            Task.CompletedTask;

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        TaskToAsyncResult.Begin(Task.CompletedTask, callback, state);

    public override int EndRead(IAsyncResult asyncResult) =>
        TaskToAsyncResult.End<int>(asyncResult);

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        TaskToAsyncResult.Begin(Task.CompletedTask, callback, state);

    public override void EndWrite(IAsyncResult asyncResult) =>
        TaskToAsyncResult.End(asyncResult);

    public override int Read(byte[] buffer, int offset, int count)
    {
        Position += count;
        return 0;
    }

    public override int Read(Span<byte> buffer)
    {
        Position += buffer.Length;
        return 0;
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Position += count;
        return cancellationToken.IsCancellationRequested?
            Task.FromCanceled<int>(cancellationToken) :
            Task.FromResult(0);
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Position += buffer.Length;
        return cancellationToken.IsCancellationRequested?
            ValueTask.FromCanceled<int>(cancellationToken) :
            default;
    }

    /// <inheritdoc/>
    public override int ReadByte()
    {
        ++Position;
        return -1;
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => Position += count;

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer) => Position += buffer.Length;

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Position += count;
        return cancellationToken.IsCancellationRequested?
            Task.FromCanceled(cancellationToken) :
            Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Position += buffer.Length;
        return cancellationToken.IsCancellationRequested?
            ValueTask.FromCanceled(cancellationToken) :
            default;
    }

    /// <inheritdoc/>
    public override void WriteByte(byte value) => ++Position;

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) =>
        Position = origin switch
                   {
                       SeekOrigin.Current => Position + offset,
                       SeekOrigin.End => Position - offset,
                       _ => offset
                   };

    /// <inheritdoc/>
    public override void SetLength(long length) { }
}
