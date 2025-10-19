using System;
using System.Security.Cryptography;

#nullable enable
namespace Hi3Helper.EncTool.Hashes;

public partial class CryptoHashUtility<T>
{
    /// <summary>
    /// Get a cryptographic-based hash from a string or chars source.
    /// </summary>
    /// <param name="source">The source of string to compute the hash from.</param>
    /// <param name="hmacKey">The secret-key used for <see cref="HMAC"/>-based Cryptographic hash.</param>
    /// <returns>The computed hash of the source.</returns>
    public byte[] GetHashFromString(ReadOnlySpan<char> source,
                                    byte[]?            hmacKey = null)
    {
        scoped Span<byte>   hashSpan = stackalloc byte[MaxHashBufferSize];
        HashOperationStatus status   = TryGetHashFromString(source, hashSpan, out int hashBytesWritten, hmacKey);

        ThrowIfStatusNonSuccess(status);

        byte[] hashReturn = new byte[hashBytesWritten];
        hashSpan[..hashBytesWritten].CopyTo(hashReturn);

        return hashReturn;
    }

    /// <summary>
    /// Get a cryptographic-based hash from byte span/array source.
    /// </summary>
    /// <param name="sourceBytes">The source of span/array to compute the hash from.</param>
    /// <param name="hmacKey">The secret-key used for <see cref="HMAC"/>-based Cryptographic hash.</param>
    /// <returns>The computed hash of the source.</returns>
    public byte[] GetHashFromBytes(ReadOnlySpan<byte> sourceBytes,
                                   byte[]?            hmacKey = null)
    {
        scoped Span<byte>   hashSpan = stackalloc byte[MaxHashBufferSize];
        HashOperationStatus status   = TryGetHashFromBytes(sourceBytes, hashSpan, out int hashBytesWritten, hmacKey);

        ThrowIfStatusNonSuccess(status);

        byte[] hashReturn = new byte[hashBytesWritten];
        hashSpan[..hashBytesWritten].CopyTo(hashReturn);

        return hashReturn;
    }

    /// <summary>
    /// Get a cryptographic-based hash from any struct-based span source.
    /// </summary>
    /// <param name="sourceAny">The source of any struct-based span to compute the hash from.</param>
    /// <param name="hmacKey">The secret-key used for <see cref="HMAC"/>-based Cryptographic hash.</param>
    /// <returns>The computed hash of the source.</returns>
    public byte[] GetHashFromAny<TValue>(ReadOnlySpan<TValue> sourceAny,
                                         byte[]?              hmacKey = null)
        where TValue : unmanaged
    {
        scoped Span<byte>   hashSpan = stackalloc byte[MaxHashBufferSize];
        HashOperationStatus status   = TryGetHashFromAny(sourceAny, hashSpan, out int hashBytesWritten, hmacKey);

        ThrowIfStatusNonSuccess(status);

        byte[] hashReturn = new byte[hashBytesWritten];
        hashSpan[..hashBytesWritten].CopyTo(hashReturn);

        return hashReturn;
    }
}
