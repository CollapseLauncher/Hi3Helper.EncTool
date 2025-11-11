using System;
using System.Buffers;
using System.IO;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Hi3Helper.EncTool.Hashes;

/// <summary>
/// Hash utility for <see cref="NonCryptographicHashAlgorithm"/>.
/// </summary>
/// <typeparam name="T">Hash type member of <see cref="NonCryptographicHashAlgorithm"/>.</typeparam>
public partial class HashUtility<T>
    where T : NonCryptographicHashAlgorithm, new()
{
    /// <summary>
    /// A shared and non thread-safe instance of <see cref="HashUtility{T}"/>.<br/>
    /// Every call to the methods of this instance is shared and cannot be called at the same time.
    /// <br/><br/>
    /// Even though we call this as "non thread-safe", if the same call is performed, the thread on that other call will be suspended/locked until
    /// the current call is finished. If you're planning to use this in parallel/multi-thread scenario, consider using <see cref="ThreadSafe"/> instance instead.
    /// </summary>
    public static HashUtility<T> Shared { get; }

    /// <summary>
    /// A non-shared and thread-safe instance of <see cref="HashUtility{T}"/>.<br/>
    /// Every inner instance of the hasher is being allocated in every call instead of being used in shared-manner.
    /// </summary>
    public static HashUtility<T> ThreadSafe { get; }

    // Reason:
    // We need to set the buffer size extremely bigger than synchronous buffer size due to async overhead.
    // Instead of hitting the Task from the ReadAsync call multiple times, we can bulk read the data in bigger
    // buffer size in one time. In our parallel benchmark scenario, we gained significant peak read speed
    // from only 1.7 GB/s to 3.1 GB/s on NVMe SSD.
    private const int  AsyncBufferSize   = 128 << 10;
    private const int  BufferSize        = 16 << 10;
    private const int  MaxStackallocSize = 128 << 10; // 128 KiB
    private const byte MaxHashBufferSize = 32; // BLAKE2 (Currently not implemented on .NET)

    // Lock for Synchronous and Semaphore for Asynchronous respectively.
    private readonly Lock?          _sharedLock;
    private readonly SemaphoreSlim? _sharedSemaphore;

    private readonly T? _sharedHasher;
    private readonly T? _sharedHasherForAsync;

    private HashUtility(bool isShared)
    {
        if (!isShared)
        {
            return;
        }

        try
        {
            _sharedHasher         = new T();
            _sharedHasherForAsync = new T();

            _sharedLock      = new Lock();
            _sharedSemaphore = new SemaphoreSlim(1, 1);
        }
        catch (PlatformNotSupportedException)
        {
            // ignored
        }
    }

    static HashUtility()
    {
        Shared     = new HashUtility<T>(true);
        ThreadSafe = new HashUtility<T>(false);
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
            hasher = new T();
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
    private async ValueTask<(T? Hasher, bool IsShared)> WaitForSemaphoreAndHasher(CancellationToken token)
    {
        if (_sharedHasherForAsync != null && _sharedSemaphore != null)
        {
            await _sharedSemaphore.WaitAsync(token);
            return (_sharedHasherForAsync, true);
        }

        try
        {
            return (new T(), false);
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
    /// Try to perform hashing from byte span/array source.
    /// </summary>
    /// <param name="sourceBytes">The source of span/array to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="hashBytesWritten">The total bytes written into the <paramref name="hashBytesDestination"/> span.</param>
    /// <returns>The hash operation status.</returns>
    public HashOperationStatus TryGetHashFromBytes(
        ReadOnlySpan<byte> sourceBytes,
        Span<byte>         hashBytesDestination,
        out int            hashBytesWritten)
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
            if (hashBytesDestination.Length < hasher.HashLengthInBytes)
            {
                return HashOperationStatus.DestinationBufferTooSmall;
            }

            // Reset state of hasher
            hasher.Reset();

            // Return hash
            hasher.Append(sourceBytes);
            return hasher.TryGetHashAndReset(hashBytesDestination, out hashBytesWritten)
                ? HashOperationStatus.Success
                : HashOperationStatus.InvalidOperation;
        }
        finally
        {
            if (isSharedMode)
            {
                lockScope.Dispose();
            }
        }
    }

    /// <summary>
    /// Try to perform hashing from any struct-based span source.
    /// </summary>
    /// <param name="sourceAny">The source of any struct-based span to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="hashBytesWritten">The total bytes written into the <paramref name="hashBytesDestination"/> span.</param>
    /// <returns>The hash operation status.</returns>
    public HashOperationStatus TryGetHashFromAny<TValue>(
        ReadOnlySpan<TValue> sourceAny,
        Span<byte>           hashBytesDestination,
        out int              hashBytesWritten)
        where TValue : unmanaged
    {
        ReadOnlySpan<byte> asByteSpanFromAny = MemoryMarshal.AsBytes(sourceAny);
        return TryGetHashFromBytes(asByteSpanFromAny, hashBytesDestination, out hashBytesWritten);
    }

    /// <summary>
    /// Try to perform hashing from a string or chars source.
    /// </summary>
    /// <param name="source">The source of string to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="hashBytesWritten">The total bytes written into the <paramref name="hashBytesDestination"/> span.</param>
    /// <returns>The hash operation status.</returns>
    public HashOperationStatus TryGetHashFromString(ReadOnlySpan<char> source,
                                                    Span<byte>         hashBytesDestination,
                                                    out int            hashBytesWritten)
    {
        // Alloc for Utf8 buffer
        int  bufByteSize   = source.Length * 2;
        bool useStackalloc = bufByteSize <= 1024;

        byte[]?           buffer     = !useStackalloc ? ArrayPool<byte>.Shared.Rent(bufByteSize) : null;
        scoped Span<byte> bufferSpan = buffer ?? stackalloc byte[bufByteSize];

        try
        {
            // Convert string to Utf8
            int bytesWritten = Encoding.UTF8.GetBytes(source, bufferSpan);
            return TryGetHashFromBytes(bufferSpan[..bytesWritten], hashBytesDestination, out hashBytesWritten);
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
    /// Try to perform hashing from <see cref="Stream"/> source.
    /// </summary>
    /// <param name="sourceStream">The source of span/array to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="hashBytesWritten">The total bytes written into the <paramref name="hashBytesDestination"/> span.</param>
    /// <param name="readBytesAction">A callback to gather how many bytes of data have been computed.</param>
    /// <param name="bufferSize">Defines the buffer size for reading data from the <see cref="Stream"/> source.</param>
    /// <param name="token">Token to notify cancellation while computing the hash.</param>
    /// <returns>The hash operation status.</returns>
    public HashOperationStatus TryGetHashFromStream(
        Stream            sourceStream,
        Span<byte>        hashBytesDestination,
        out int           hashBytesWritten,
        Action<int>?      readBytesAction = null,
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
                                        bufferSize,
                                        token);
        }
        finally
        {
            if (isSharedMode)
            {
                lockScope.Dispose();
            }
        }
    }

    /// <summary>
    /// Try to perform hashing from <see cref="Stream"/> source using a custom <see cref="NonCryptographicHashAlgorithm"/> instance.
    /// </summary>
    /// <param name="hasher">A custom <see cref="NonCryptographicHashAlgorithm"/> instance for hashing.</param>
    /// <param name="sourceStream">The source of span/array to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="hashBytesWritten">The total bytes written into the <paramref name="hashBytesDestination"/> span.</param>
    /// <param name="readBytesAction">A callback to gather how many bytes of data have been computed.</param>
    /// <param name="bufferSize">Defines the buffer size for reading data from the <see cref="Stream"/> source.</param>
    /// <param name="token">Token to notify cancellation while computing the hash.</param>
    /// <returns>The hash operation status.</returns>
    public HashOperationStatus TryGetHashFromStream(
        NonCryptographicHashAlgorithm hasher,
        Stream                        sourceStream,
        Span<byte>                    hashBytesDestination,
        out int                       hashBytesWritten,
        Action<int>?                  readBytesAction = null,
        int                           bufferSize      = BufferSize,
        CancellationToken             token           = default)
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
            if (hashBytesDestination.Length < hasher.HashLengthInBytes)
            {
                return HashOperationStatus.DestinationBufferTooSmall;
            }

            // Reset state of hasher
            hasher.Reset();

            // Alloc buffer
            buffer = bufferSize > MaxStackallocSize
                ? ArrayPool<byte>.Shared.Rent(bufferSize)
                : null;
            Span<byte> bufferSpan = buffer ?? stackalloc byte[bufferSize];

            // Compute hash
            int read;
            while ((read = sourceStream.ReadAtLeast(bufferSpan, bufferSize, false)) > 0)
            {
                token.ThrowIfCancellationRequested();
                hasher.Append(bufferSpan[..read]);
                readBytesAction?.Invoke(read);
            }

            // Return hash
            return !hasher.TryGetHashAndReset(hashBytesDestination, out hashBytesWritten)
                ? HashOperationStatus.InvalidOperation
                : HashOperationStatus.Success;
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
    /// Try to asynchronously perform hashing from <see cref="Stream"/> source.
    /// </summary>
    /// <param name="sourceStream">The source of span/array to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="readBytesAction">A callback to gather how many bytes of data have been computed.</param>
    /// <param name="bufferSize">Defines the buffer size for reading data from the <see cref="Stream"/> source.</param>
    /// <param name="token">Token to notify cancellation while computing the hash.</param>
    /// <returns>Returns a Value Tuple of <see cref="HashOperationStatus"/> and length of bytes written to <paramref name="hashBytesDestination"/>.</returns>
    public async Task<(HashOperationStatus Status, int HashBytesWritten)>
        TryGetHashFromStreamAsync(Stream            sourceStream,
                                  Memory<byte>      hashBytesDestination,
                                  Action<int>?      readBytesAction = null,
                                  int               bufferSize      = AsyncBufferSize,
                                  CancellationToken token           = default)
    {
        // Enter the lock scope and acquire hasher
        (T? hasher, bool isSharedMode) = await WaitForSemaphoreAndHasher(token);
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
                                                   bufferSize,
                                                   token);
        }
        finally
        {
            if (isSharedMode)
            {
                _sharedSemaphore?.Release();
            }
        }
    }

    /// <summary>
    /// Try to asynchronously perform hashing from <see cref="Stream"/> source using a custom <see cref="NonCryptographicHashAlgorithm"/> instance.
    /// </summary>
    /// <param name="hasher">A custom <see cref="NonCryptographicHashAlgorithm"/> instance for hashing.</param>
    /// <param name="sourceStream">The source of span/array to compute the hash from.</param>
    /// <param name="hashBytesDestination">The buffer where the hash result will be written.</param>
    /// <param name="readBytesAction">A callback to gather how many bytes of data have been computed.</param>
    /// <param name="bufferSize">Defines the buffer size for reading data from the <see cref="Stream"/> source.</param>
    /// <param name="token">Token to notify cancellation while computing the hash.</param>
    /// <returns>Returns a Value Tuple of <see cref="HashOperationStatus"/> and length of bytes written to <paramref name="hashBytesDestination"/>.</returns>
    public Task<(HashOperationStatus Status, int HashBytesWritten)>
        TryGetHashFromStreamAsync(NonCryptographicHashAlgorithm hasher,
                                  Stream                        sourceStream,
                                  Memory<byte>                  hashBytesDestination,
                                  Action<int>?                  readBytesAction = null,
                                  int                           bufferSize      = AsyncBufferSize,
                                  CancellationToken             token           = default)
    {
        if (token.IsCancellationRequested)
        {
            return Task.FromResult((HashOperationStatus.OperationCancelled, 0));
        }

        TaskCompletionSource<(HashOperationStatus Status, int HashBytesWritten)> tcs =
            new TaskCompletionSource<(HashOperationStatus Status, int HashBytesWritten)>();

        Task.Factory.StartNew(Worker, token);
        return tcs.Task;

        void Worker()
        {
            // Ensure buffer size is valid
            if (bufferSize <= 0)
            {
                bufferSize = AsyncBufferSize;
            }

            byte[]? buffer = null;
            try
            {
                if (hashBytesDestination.Length < hasher.HashLengthInBytes)
                {
                    tcs.SetResult((HashOperationStatus.DestinationBufferTooSmall, 0));
                    return;
                }

                // Reset state of hasher
                hasher.Reset();

                // Alloc buffer
                buffer = bufferSize > MaxStackallocSize
                    ? ArrayPool<byte>.Shared.Rent(bufferSize)
                    : null;
                Span<byte> bufferSpan = buffer ?? stackalloc byte[bufferSize];

                try
                {
                    int read;
                    while ((read = sourceStream.ReadAtLeast(bufferSpan, bufferSize, false)) > 0)
                    {
                        hasher.Append(bufferSpan[..read]);
                        readBytesAction?.Invoke(read);
                        token.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException)
                {
                    tcs.SetResult((HashOperationStatus.OperationCancelled, 0));
                    return;
                }

                // Return hash
                tcs.SetResult(!hasher.TryGetHashAndReset(hashBytesDestination.Span, out int hashLengthWritten)
                                  ? (HashOperationStatus.InvalidOperation, 0)
                                  : (HashOperationStatus.Success, hashLengthWritten));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
            finally
            {
                if (buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
    }
}
