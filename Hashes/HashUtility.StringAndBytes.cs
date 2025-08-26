using System;

#nullable enable
namespace Hi3Helper.EncTool.Hashes;

public partial class HashUtility<T>
{
    /// <summary>
    /// Get a hash from a string or chars source.
    /// </summary>
    /// <param name="source">The source of string to compute the hash from.</param>
    /// <returns>The computed hash of the source.</returns>
    public byte[] GetHashFromString(ReadOnlySpan<char> source)
    {
        scoped Span<byte> hashSpan = stackalloc byte[MaxHashBufferSize];
        HashOperationStatus status = TryGetHashFromString(source, hashSpan, out int hashBytesWritten);

        ThrowIfStatusNonSuccess(status);

        byte[] hashReturn = new byte[hashBytesWritten];
        hashSpan[..hashBytesWritten].CopyTo(hashReturn);

        return hashReturn;
    }

    /// <summary>
    /// Get a hash from byte span/array source.
    /// </summary>
    /// <param name="sourceBytes">The source of span/array to compute the hash from.</param>
    /// <returns>The computed hash of the source.</returns>
    public byte[] GetHashFromBytes(ReadOnlySpan<byte> sourceBytes)
    {
        scoped Span<byte> hashSpan = stackalloc byte[MaxHashBufferSize];
        HashOperationStatus status = TryGetHashFromBytes(sourceBytes, hashSpan, out int hashBytesWritten);

        ThrowIfStatusNonSuccess(status);

        byte[] hashReturn = new byte[hashBytesWritten];
        hashSpan[..hashBytesWritten].CopyTo(hashReturn);

        return hashReturn;
    }

    /// <summary>
    /// Get a hash from any struct-based span source.
    /// </summary>
    /// <param name="sourceAny">The source of any struct-based span to compute the hash from.</param>
    /// <returns>The computed hash of the source.</returns>
    public byte[] GetHashFromAny<TValue>(ReadOnlySpan<TValue> sourceAny)
        where TValue : unmanaged
    {
        scoped Span<byte> hashSpan = stackalloc byte[MaxHashBufferSize];
        HashOperationStatus status = TryGetHashFromAny(sourceAny, hashSpan, out int hashBytesWritten);

        ThrowIfStatusNonSuccess(status);

        byte[] hashReturn = new byte[hashBytesWritten];
        hashSpan[..hashBytesWritten].CopyTo(hashReturn);

        return hashReturn;
    }
}
