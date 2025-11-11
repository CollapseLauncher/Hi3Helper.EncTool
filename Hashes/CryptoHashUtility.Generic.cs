using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable StringLiteralTypo

#nullable enable
namespace Hi3Helper.EncTool.Hashes;

/// <summary>
/// Hash utility for Cryptographic-based <see cref="HashAlgorithm"/>.
/// </summary>
/// <typeparam name="T">Hash type member of Cryptographic-based <see cref="HashAlgorithm"/>.</typeparam>
public partial class CryptoHashUtility<T>
    where T : HashAlgorithm
{
    /// <summary>
    /// A shared and non thread-safe instance of <see cref="CryptoHashUtility{T}"/>.<br/>
    /// Every call to the methods of this instance is shared and cannot be called at the same time.
    /// <br/><br/>
    /// Even though we call this as "non thread-safe", if the same call is performed, the thread on that other call will be suspended/locked until
    /// the current call is finished. If you're planning to use this in parallel/multi-thread scenario, consider using <see cref="ThreadSafe"/> instance instead.
    /// </summary>
    public static CryptoHashUtility<T> Shared { get; }

    /// <summary>
    /// A non-shared and thread-safe instance of <see cref="CryptoHashUtility{T}"/>.<br/>
    /// Every inner instance of the hasher is being allocated in every call instead of being used in shared-manner.
    /// </summary>
    public static CryptoHashUtility<T> ThreadSafe { get; }

    // Reason:
    // We need to set the buffer size extremely bigger than synchronous buffer size due to async overhead.
    // Instead of hitting the Task from the ReadAsync call multiple times, we can bulk read the data in bigger
    // buffer size in one time. In our parallel benchmark scenario, we gained significant peak read speed
    // from only 1.7 GB/s to 3.1 GB/s on NVMe SSD.
    private const int  AsyncBufferSize   = 128 << 10;
    private const int  BufferSize        = 16 << 10;
    private const int  MaxStackallocSize = 128 << 10;              // 128 KiB
    private const byte MaxHashBufferSize = SHA512.HashSizeInBytes; // SHA512

    // Lock for Synchronous and Semaphore for Asynchronous respectively.
    private readonly Lock?          _sharedLock;
    private readonly SemaphoreSlim? _sharedSemaphore;

    private readonly T? _sharedHasher;
    private readonly T? _sharedHasherForAsync;

    private CryptoHashUtility(bool isShared)
    {
        if (!isShared)
        {
            return;
        }

        try
        {
            _sharedHasher         = CreateInstance();
            _sharedHasherForAsync = CreateInstance();

            _sharedLock      = new Lock();
            _sharedSemaphore = new SemaphoreSlim(1, 1);
        }
        catch (PlatformNotSupportedException)
        {
            // ignored
        }
    }

    static CryptoHashUtility()
    {
        Shared     = new CryptoHashUtility<T>(true);
        ThreadSafe = new CryptoHashUtility<T>(false);
    }

    /// <summary>
    /// Create the hash instance of <see cref="HashAlgorithm"/>.
    /// </summary>
    private static T CreateInstance()
    {
        string nameOf = typeof(T).Name;
        return (T)(object)(nameOf switch
               {
                   "HMACMD5" => new HMACMD5(),
                   "HMACSHA1" => new HMACSHA1(),
                   "HMACSHA256" => new HMACSHA256(),
                   "HMACSHA384" => new HMACSHA384(),
                   "HMACSHA512" => new HMACSHA512(),
                   "HMACSHA3_256" => new HMACSHA3_256(),
                   "HMACSHA3_384" => new HMACSHA3_384(),
                   "HMACSHA3_512" => new HMACSHA3_256(),

                   "MD5" => MD5.Create(),
                   "SHA1" => SHA1.Create(),
                   "SHA256" => SHA256.Create(),
                   "SHA384" => SHA384.Create(),
                   "SHA512" => SHA512.Create(),
                   "SHA3_256" => SHA3_256.Create(),
                   "SHA3_384" => SHA3_384.Create(),
                   "SHA3_512" => SHA3_512.Create(),
                   _ => throw new
                       NotSupportedException($"Hash type: {nameOf} isn't supported! Please manually add the switch entry.")
               });
    }

    /// <summary>
    /// Returns true if shared mode is used. Otherwise, false.
    /// </summary>
    private bool TryAcquireLockAndHasher(out Lock.Scope lockScope, out T? hasher)
    {
        if (_sharedHasher != null && _sharedLock != null)
        {
            lockScope = _sharedLock.EnterScope();
            hasher    = _sharedHasher;
            return true;
        }

        lockScope = default;
        try
        {
            hasher = CreateInstance();
        }
        catch (PlatformNotSupportedException)
        {
            // ignored
            hasher = null;
        }
        return false;
    }

    /// <summary>
    /// Wait until shared semaphore is released, locks and return the hasher for asynchronous operation.
    /// </summary>
    /// <returns>The hasher to be used.</returns>
    private async ValueTask<(HashAlgorithm? Hasher, bool IsShared)> WaitForSemaphoreAndHasher(CancellationToken token)
    {
        if (_sharedHasherForAsync != null && _sharedSemaphore != null)
        {
            await _sharedSemaphore.WaitAsync(token);
            return (_sharedHasherForAsync, true);
        }

        try
        {
            return (CreateInstance(), false);
        }
        catch (PlatformNotSupportedException)
        {
            // ignored
            return (null, _sharedHasherForAsync != null && _sharedSemaphore != null);
        }
    }

    /// <summary>
    /// Throw if the status is not success, with cancellation token support.
    /// </summary>
    private static void ThrowIfStatusNonSuccess(HashOperationStatus status, CancellationToken token)
    {
        if (status == HashOperationStatus.OperationCancelled)
        {
            if (token.IsCancellationRequested)
            {
                throw new OperationCanceledException("Hash operation was cancelled by the user!");
            }

            throw new OperationCanceledException("Hash operation was cancelled due to external cancellation request or timed-out!");
        }

        ThrowIfStatusNonSuccess(status);
    }

    /// <summary>
    /// Throw if the status is not success.
    /// </summary>
    private static void ThrowIfStatusNonSuccess(HashOperationStatus status)
    {
        if (status != HashOperationStatus.Success)
        {
            throw new InvalidOperationException($"Hashing operation failed with status: {status}");
        }
    }

    /// <summary>
    /// Try to perform cryptographic-based hashing from byte span/array source.
    /// </summary>
    /// <param name="sourceBytes">The source of span/array to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="hashBytesWritten">The total bytes written into the <paramref name="hashBytesDestination"/> span.</param>
    /// <param name="hmacKey">The secret-key used for <see cref="HMAC"/>-based Cryptographic hash.</param>
    /// <remarks>
    /// When using <see cref="HMAC"/>-based Cryptographic hash, the <paramref name="hmacKey"/> must be provided.
    /// Otherwise, it will return <see cref="HashOperationStatus.InvalidOperation"/>.
    /// </remarks>
    /// <returns>The hash operation status.</returns>
    public HashOperationStatus TryGetHashFromBytes(
        ReadOnlySpan<byte> sourceBytes,
        Span<byte>         hashBytesDestination,
        out int            hashBytesWritten,
        byte[]?            hmacKey = null)
    {
        // Enter the lock scope and acquire hasher
        hashBytesWritten = 0;
        bool isSharedMode = TryAcquireLockAndHasher(out Lock.Scope lockScope, out T? hasher);

        if (hasher == null)
        {
            lockScope.Dispose();
            return HashOperationStatus.HashNotSupported;
        }

        try
        {
            if (hashBytesDestination.Length < hasher.HashSize >> 3)
            {
                return HashOperationStatus.DestinationBufferTooSmall;
            }

            // Reset state of hasher
            hasher.Initialize();
            if (hmacKey is not { Length: > 0 }) // Sanity check and apply HMAC key if applied
            {
                return hasher.TryComputeHash(sourceBytes, hashBytesDestination, out hashBytesWritten)
                    ? HashOperationStatus.Success
                    : HashOperationStatus.InvalidOperation;
            }

            if (hasher is not HMAC asHmacHash)
            {
                return HashOperationStatus.InvalidOperation;
            }

            asHmacHash.Key = hmacKey;

            return hasher.TryComputeHash(sourceBytes, hashBytesDestination, out hashBytesWritten)
                ? HashOperationStatus.Success
                : HashOperationStatus.InvalidOperation;
        }
        finally
        {
            if (isSharedMode)
            {
                lockScope.Dispose();
            }
            else
            {
                hasher.Dispose();
            }
        }
    }

    /// <summary>
    /// Try to perform cryptographic-based hashing from any struct-based span source.
    /// </summary>
    /// <param name="sourceAny">The source of any struct-based span to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="hashBytesWritten">The total bytes written into the <paramref name="hashBytesDestination"/> span.</param>
    /// <param name="hmacKey">The secret-key used for <see cref="HMAC"/>-based Cryptographic hash.</param>
    /// <remarks>
    /// When using <see cref="HMAC"/>-based Cryptographic hash, the <paramref name="hmacKey"/> must be provided.
    /// Otherwise, it will return <see cref="HashOperationStatus.InvalidOperation"/>.
    /// </remarks>
    /// <returns>The hash operation status.</returns>
    public HashOperationStatus TryGetHashFromAny<TValue>(
        ReadOnlySpan<TValue> sourceAny,
        Span<byte>           hashBytesDestination,
        out int              hashBytesWritten,
        byte[]?              hmacKey = null)
        where TValue : unmanaged
    {
        ReadOnlySpan<byte> asByteSpanFromAny = MemoryMarshal.AsBytes(sourceAny);
        return TryGetHashFromBytes(asByteSpanFromAny, hashBytesDestination, out hashBytesWritten, hmacKey);
    }

    /// <summary>
    /// Try to perform cryptographic-based hashing from a string or chars source.
    /// </summary>
    /// <param name="source">The source of string to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="hashBytesWritten">The total bytes written into the <paramref name="hashBytesDestination"/> span.</param>
    /// <param name="hmacKey">The secret-key used for <see cref="HMAC"/>-based Cryptographic hash.</param>
    /// <remarks>
    /// When using <see cref="HMAC"/>-based Cryptographic hash, the <paramref name="hmacKey"/> must be provided.
    /// Otherwise, it will return <see cref="HashOperationStatus.InvalidOperation"/>.
    /// </remarks>
    /// <returns>The hash operation status.</returns>
    public HashOperationStatus TryGetHashFromString(ReadOnlySpan<char> source,
                                                    Span<byte>         hashBytesDestination,
                                                    out int            hashBytesWritten,
                                                    byte[]?            hmacKey = null)
    {
        // Alloc for Utf8 buffer
        int  bufByteSize   = source.Length * 2;
        bool useStackalloc = bufByteSize <= MaxStackallocSize;

        byte[]?           buffer     = !useStackalloc ? ArrayPool<byte>.Shared.Rent(bufByteSize) : null;
        scoped Span<byte> bufferSpan = buffer ?? stackalloc byte[bufByteSize];

        try
        {
            // Convert string to Utf8
            int bytesWritten = Encoding.UTF8.GetBytes(source, bufferSpan);
            return TryGetHashFromBytes(bufferSpan[..bytesWritten], hashBytesDestination, out hashBytesWritten, hmacKey);
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// Try to perform cryptographic-based hashing from <see cref="Stream"/> source.
    /// </summary>
    /// <param name="sourceStream">The source of span/array to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="readBytesAction">A callback to gather how many bytes of data have been computed.</param>
    /// <param name="hashBytesWritten">The total bytes written into the <paramref name="hashBytesDestination"/> span.</param>
    /// <param name="hmacKey">The secret-key used for <see cref="HMAC"/>-based Cryptographic hash.</param>
    /// <param name="bufferSize">Defines the buffer size for reading data from the <see cref="Stream"/> source.</param>
    /// <param name="token">Token to notify cancellation while computing the hash.</param>
    /// <remarks>
    /// When using <see cref="HMAC"/>-based Cryptographic hash, the <paramref name="hmacKey"/> must be provided.
    /// Otherwise, it will return <see cref="HashOperationStatus.InvalidOperation"/>.
    /// </remarks>
    /// <returns>The hash operation status.</returns>
    public HashOperationStatus TryGetHashFromStream(
        Stream            sourceStream,
        Span<byte>        hashBytesDestination,
        out int           hashBytesWritten,
        Action<int>?      readBytesAction = null,
        byte[]?           hmacKey         = null,
        int               bufferSize      = BufferSize,
        CancellationToken token           = default)
    {
        hashBytesWritten = 0;

        // Enter the lock scope and acquire hasher
        bool isSharedMode = TryAcquireLockAndHasher(out Lock.Scope lockScope, out T? hasher);
        if (hasher == null)
        {
            lockScope.Dispose();
            return HashOperationStatus.HashNotSupported;
        }

        try
        {
            return TryGetHashFromStream(hasher,
                                        sourceStream,
                                        hashBytesDestination,
                                        out hashBytesWritten,
                                        readBytesAction,
                                        hmacKey,
                                        bufferSize,
                                        true,
                                        token);
        }
        finally
        {
            if (isSharedMode)
            {
                lockScope.Dispose();
            }
            else
            {
                hasher.Dispose();
            }
        }
    }

    /// <summary>
    /// Try to perform cryptographic-based hashing from <see cref="Stream"/> source using a custom <see cref="HashAlgorithm"/> instance.
    /// </summary>
    /// <param name="hasher">A custom <see cref="HashAlgorithm"/> instance for hashing.</param>
    /// <param name="sourceStream">The source of span/array to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="readBytesAction">A callback to gather how many bytes of data have been computed.</param>
    /// <param name="hashBytesWritten">The total bytes written into the <paramref name="hashBytesDestination"/> span.</param>
    /// <param name="hmacKey">The secret-key used for <see cref="HMAC"/>-based Cryptographic hash.</param>
    /// <param name="bufferSize">Defines the buffer size for reading data from the <see cref="Stream"/> source.</param>
    /// <param name="disposeHasher">Whether to dispose the <paramref name="hasher"/> after operation or not.</param>
    /// <param name="token">Token to notify cancellation while computing the hash.</param>
    /// <remarks>
    /// When using <see cref="HMAC"/>-based Cryptographic hash, the <paramref name="hmacKey"/> must be provided.
    /// Otherwise, it will return <see cref="HashOperationStatus.InvalidOperation"/>.
    /// </remarks>
    /// <returns>The hash operation status.</returns>
    public HashOperationStatus TryGetHashFromStream(
        HashAlgorithm     hasher,
        Stream            sourceStream,
        Span<byte>        hashBytesDestination,
        out int           hashBytesWritten,
        Action<int>?      readBytesAction = null,
        byte[]?           hmacKey         = null,
        int               bufferSize      = BufferSize,
        bool              disposeHasher   = false,
        CancellationToken token           = default)
    {
        hashBytesWritten = 0;
        if (token.IsCancellationRequested)
        {
            return HashOperationStatus.OperationCancelled;
        }

        // Ensure buffer size is valid
        if (bufferSize <= 0)
        {
            bufferSize = BufferSize;
        }

        byte[]? buffer = null;
        try
        {

            if (hashBytesDestination.Length < hasher.HashSize >> 3)
            {
                return HashOperationStatus.DestinationBufferTooSmall;
            }

            // Reset state of hasher
            hasher.Initialize();
            if (hmacKey is { Length: > 0 }) // Sanity check and apply HMAC key if applied
            {
                if (hasher is not HMAC asHmacHash)
                {
                    return HashOperationStatus.InvalidOperation;
                }

                asHmacHash.Key = hmacKey;
            }

            // Compute hash
            buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            int read;

            while ((read = sourceStream.ReadAtLeast(buffer, bufferSize, false)) > 0)
            {
                token.ThrowIfCancellationRequested();
                hasher.TransformBlock(buffer, 0, read, buffer, 0);
                readBytesAction?.Invoke(read);
            }

            hasher.TransformFinalBlock(buffer, 0, read);
            if (read > 0)
            {
                readBytesAction?.Invoke(read);
            }

            // Return hash
            ReadOnlySpan<byte> hashSpan = hasher.Hash;
            hashBytesWritten = hashSpan.Length;
            hashSpan.CopyTo(hashBytesDestination);

            return HashOperationStatus.Success;
        }
        finally
        {
            if (disposeHasher)
            {
                hasher.Dispose();
            }

            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// Try to asynchronously perform cryptographic-based hashing from <see cref="Stream"/> source.
    /// </summary>
    /// <param name="sourceStream">The source of span/array to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="readBytesAction">A callback to gather how many bytes of data have been computed.</param>
    /// <param name="hmacKey">The secret-key used for <see cref="HMAC"/>-based Cryptographic hash.</param>
    /// <param name="bufferSize">Defines the buffer size for reading data from the <see cref="Stream"/> source.</param>
    /// <param name="token">Token to notify cancellation while computing the hash.</param>
    /// <remarks>
    /// When using <see cref="HMAC"/>-based Cryptographic hash, the <paramref name="hmacKey"/> must be provided.
    /// Otherwise, it will return <see cref="HashOperationStatus.InvalidOperation"/>.
    /// </remarks>
    /// <returns>Returns a Value Tuple of <see cref="HashOperationStatus"/> and length of bytes written to <paramref name="hashBytesDestination"/>.</returns>
    public async Task<(HashOperationStatus Status, int HashBytesWritten)>
        TryGetHashFromStreamAsync(Stream            sourceStream,
                                  Memory<byte>      hashBytesDestination,
                                  Action<int>?      readBytesAction = null,
                                  byte[]?           hmacKey         = null,
                                  int               bufferSize      = AsyncBufferSize,
                                  CancellationToken token           = default)
    {
        // Enter the lock scope and acquire hasher
        (HashAlgorithm? hasher, bool isSharedMode) = await WaitForSemaphoreAndHasher(token);
        if (hasher == null)
        {
            if (isSharedMode)
            {
                // Release semaphore early if shared mode.
                _sharedSemaphore?.Release();
            }

            return (HashOperationStatus.HashNotSupported, 0);
        }

        try
        {
            return await TryGetHashFromStreamAsync(hasher,
                                                   sourceStream,
                                                   hashBytesDestination,
                                                   readBytesAction,
                                                   hmacKey,
                                                   bufferSize,
                                                   !isSharedMode,
                                                   token);
        }
        finally
        {
            if (isSharedMode)
            {
                _sharedSemaphore?.Release();
            }
            else
            {
                hasher.Dispose();
            }
        }
    }

    /// <summary>
    /// Try to asynchronously perform cryptographic-based hashing from <see cref="Stream"/> source using a custom <see cref="HashAlgorithm"/> instance.
    /// </summary>
    /// <param name="hasher">A custom <see cref="HashAlgorithm"/> instance for hashing.</param>
    /// <param name="sourceStream">The source of span/array to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="readBytesAction">A callback to gather how many bytes of data have been computed.</param>
    /// <param name="hmacKey">The secret-key used for <see cref="HMAC"/>-based Cryptographic hash.</param>
    /// <param name="bufferSize">Defines the buffer size for reading data from the <see cref="Stream"/> source.</param>
    /// <param name="disposeHasher">Whether to dispose the <paramref name="hasher"/> after operation or not.</param>
    /// <param name="token">Token to notify cancellation while computing the hash.</param>
    /// <remarks>
    /// When using <see cref="HMAC"/>-based Cryptographic hash, the <paramref name="hmacKey"/> must be provided.
    /// Otherwise, it will return <see cref="HashOperationStatus.InvalidOperation"/>.
    /// </remarks>
    /// <returns>Returns a Value Tuple of <see cref="HashOperationStatus"/> and length of bytes written to <paramref name="hashBytesDestination"/>.</returns>
    public async Task<(HashOperationStatus Status, int HashBytesWritten)>
        TryGetHashFromStreamAsync(
            HashAlgorithm     hasher,
            Stream            sourceStream,
            Memory<byte>      hashBytesDestination,
            Action<int>?      readBytesAction = null,
            byte[]?           hmacKey         = null,
            int               bufferSize      = AsyncBufferSize,
            bool              disposeHasher   = false,
            CancellationToken token           = default)
    {
        if (token.IsCancellationRequested)
        {
            return (HashOperationStatus.OperationCancelled, 0);
        }

        // Ensure buffer size is valid
        if (bufferSize <= 0)
        {
            bufferSize = AsyncBufferSize;
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            if (hashBytesDestination.Length < hasher.HashSize >> 3)
            {
                return (HashOperationStatus.DestinationBufferTooSmall, 0);
            }

            // Reset state of hasher
            hasher.Initialize();
            if (hmacKey is { Length: > 0 }) // Sanity check and apply HMAC key if applied
            {
                if (hasher is not HMAC asHmacHash)
                {
                    return (HashOperationStatus.InvalidOperation, 0);
                }

                asHmacHash.Key = hmacKey;
            }

            // Compute hash
            int read;

            try
            {
                while ((read = await sourceStream
                                    .ReadAtLeastAsync(buffer, bufferSize, false, token)) > 0)
                {
                    hasher.TransformBlock(buffer, 0, read, buffer, 0);
                    readBytesAction?.Invoke(read);
                }
            }
            catch (OperationCanceledException)
            {
                return (HashOperationStatus.OperationCancelled, 0);
            }

            hasher.TransformFinalBlock(buffer, 0, read);
            if (read > 0)
            {
                readBytesAction?.Invoke(read);
            }

            // Return hash
            ReadOnlySpan<byte> hashSpan = hasher.Hash;
            hashSpan.CopyTo(hashBytesDestination.Span);

            return (HashOperationStatus.Success, hashSpan.Length);
        }
        finally
        {
            if (disposeHasher)
            {
                hasher.Dispose();
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
