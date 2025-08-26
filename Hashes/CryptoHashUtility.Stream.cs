using System;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using System.Threading.Tasks;

#nullable enable
namespace Hi3Helper.EncTool.Hashes;

public partial class CryptoHashUtility<T>
{
    /// <summary>
    /// Get a cryptographic-based hash from a <see cref="Stream"/> source.
    /// </summary>
    /// <param name="sourceStream">The source of any struct-based span to compute the hash from.</param>
    /// <param name="readBytesAction">A callback to gather how many bytes of data have been computed.</param>
    /// <param name="hmacKey">The secret-key used for <see cref="HMAC"/>-based Cryptographic hash.</param>
    /// <param name="bufferSize">Defines the buffer size for reading data from the <see cref="Stream"/> source.</param>
    /// <param name="token">Token to notify cancellation while computing the hash.</param>
    /// <returns>The computed hash of the source.</returns>
    public byte[] GetHashFromStream(Stream            sourceStream,
                                    Action<int>?      readBytesAction = null,
                                    byte[]?           hmacKey         = null,
                                    int               bufferSize      = BufferSize,
                                    CancellationToken token           = default)
    {
        scoped Span<byte> hashSpan = stackalloc byte[MaxHashBufferSize];
        HashOperationStatus status = TryGetHashFromStream(sourceStream,
                                                          hashSpan,
                                                          out int hashBytesWritten,
                                                          readBytesAction,
                                                          hmacKey,
                                                          bufferSize,
                                                          token);

        ThrowIfStatusNonSuccess(status, token);

        byte[] hashReturn = new byte[hashBytesWritten];
        hashSpan[..hashBytesWritten].CopyTo(hashReturn);

        return hashReturn;
    }

    /// <summary>
    /// Asynchronously get a cryptographic-based hash from a <see cref="Stream"/> source.
    /// </summary>
    /// <param name="sourceStream">The source of any struct-based span to compute the hash from.</param>
    /// <param name="readBytesAction">A callback to gather how many bytes of data have been computed.</param>
    /// <param name="hmacKey">The secret-key used for <see cref="HMAC"/>-based Cryptographic hash.</param>
    /// <param name="bufferSize">Defines the buffer size for reading data from the <see cref="Stream"/> source.</param>
    /// <param name="token">Token to notify cancellation while computing the hash.</param>
    /// <returns>The computed hash of the source.</returns>
    public async Task<byte[]> GetHashFromStreamAsync(Stream            sourceStream,
                                                     Action<int>?      readBytesAction = null,
                                                     byte[]?           hmacKey         = null,
                                                     int               bufferSize      = BufferSize,
                                                     CancellationToken token           = default)
    {
        byte[] hashArray = new byte[MaxHashBufferSize];
        (HashOperationStatus status, int hashBytesWritten) =
            await TryGetHashFromStreamAsync(sourceStream,
                                            hashArray,
                                            readBytesAction,
                                            hmacKey,
                                            bufferSize,
                                            token);

        ThrowIfStatusNonSuccess(status, token);

        byte[] hashReturn = new byte[hashBytesWritten];
        hashArray.AsSpan(0, hashBytesWritten).CopyTo(hashReturn);

        return hashReturn;
    }
}
