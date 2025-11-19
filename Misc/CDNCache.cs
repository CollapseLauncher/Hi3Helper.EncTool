using Hi3Helper.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable CA1873

// ReSharper disable CommentTypo
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace

#nullable enable
namespace Hi3Helper.EncTool;

public class CDNCache
{
    /// <summary>
    /// Gets/sets the current directory for storing the cache locally.<br/>
    /// While the cache directory is set, this will also perform a cache garbage collection based on <see cref="MaxAcceptedCacheExpireTime"/>.<br/>
    /// <br/>
    /// To skip the cache garbage collection, use <see cref="SetCacheDirSkipGC"/>.
    /// </summary>
    public string? CurrentCacheDir
    {
        get;
        set => field = PerformCacheGarbageCollection(value);
    }

    /// <summary>
    /// Gets/sets the logger for this cache utility.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Whether the CDN cache is enabled or not.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether the aggressive mode is enabled or not (Default: disabled).<br/>
    /// This mode will try to cache all requests, even if the ETag or expire header is not present in the response.<br/>
    /// <br/>
    /// This method is valid only if <see cref="IsEnabled"/> is set to true and if <see cref="TryGetCachedStreamFrom(HttpClient,string,HttpMethod,System.Threading.CancellationToken)"/> method is used.<br/>
    /// </summary>
    public bool IsUseAggressiveMode { get; set; }

    /// <summary>
    /// Determine how long the maximum duration of the cache expire time is accepted (Default: 10 minutes).<br/>
    /// This will clamp the maximum duration of the cache expire time, even if the CDN provides a longer duration.<br/>
    /// <br/>
    /// This also sets how long the cache will be kept in the local directory before it is cleaned up, either it's hash-based or time-based cache.<br/>
    /// </summary>
    public TimeSpan MaxAcceptedCacheExpireTime { get; set; } = TimeSpan.FromMinutes(10);

    #region Private Fields
    private readonly Lock            ThisLock             = new();
    private readonly HashSet<string> CurrentETagToWrite   = [];
    private const    int             MaxHashLengthInBytes = SHA256.HashSizeInBytes;

    private const string SkipGCPrefix = "Skip|";
    private const string LetterNumberOnlyAscii = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const string SymbolOnlyAscii = " !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
    private SearchValues<char> LetterNumberOnlyAsciiSearchValue { get; } = SearchValues.Create(LetterNumberOnlyAscii);
    private SearchValues<char> SymbolOnlyAsciiSearchValue { get; } = SearchValues.Create(SymbolOnlyAscii);
    #endregion

    [Flags]
    private enum HashCacheType
    {
        None       = 0,
        CryptoType = 0b_00000001_00000000,

        Crc32  = 0b_00000000_00000001,
        Crc64  = 0b_00000000_00000010,
        MD5    = CryptoType | 0b_00000000_00000100,
        Sha1   = CryptoType | 0b_00000000_00001000,
        Sha256 = CryptoType | 0b_00000000_00010000
    }

    /// <summary>
    /// Set the cache directory to use for the CDN cache and skip garbage collection.<br/>
    /// </summary>
    /// <param name="cacheDir">The cache directory to use.</param>
    public void SetCacheDirSkipGC(string? cacheDir)
    {
        if (!string.IsNullOrEmpty(cacheDir))
        {
            cacheDir = SkipGCPrefix + cacheDir;
        }
        CurrentCacheDir = cacheDir;
    }

    /// <summary>
    /// Gets the cached response status from the given URL using HEAD request. The return value: <see cref="UrlStatus"/> contains the HTTP status code and size of the response.
    /// </summary>
    /// <param name="client">Client to be used to retrieve the response.</param>
    /// <param name="url">The URL to check</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The return status of the given URL.</returns>
    public ValueTask<UrlStatus> GetCachedUrlStatus(
        HttpClient        client,
        string            url,
        CancellationToken token) =>
        GetCachedUrlStatus(client, new Uri(url), token);

    /// <summary>
    /// Gets the cached response status from the given URL using HEAD request. The return value: <see cref="UrlStatus"/> contains the HTTP status code and size of the response.
    /// </summary>
    /// <param name="client">Client to be used to retrieve the response.</param>
    /// <param name="url">The URL to check</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The return status of the given URL.</returns>
    public async ValueTask<UrlStatus> GetCachedUrlStatus(
        HttpClient        client,
        Uri               url,
        CancellationToken token)
    {
        if (!IsEnabled || string.IsNullOrEmpty(CurrentCacheDir))
        {
            using HttpResponseMessage uncachedResponse =
                await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url),
                                       HttpCompletionOption.ResponseHeadersRead,
                                       token)
                            .ConfigureAwait(false);
            return new UrlStatus(uncachedResponse);
        }

        string   cacheDir           = CurrentCacheDir;
        string   cacheStampName     = $"cached_status_{GetXxh3HashFrom(url.AbsoluteUri.AsSpan())}";
        string   cacheStampPath     = Path.Combine(cacheDir, cacheStampName);
        FileInfo cacheStampFileInfo = new(cacheStampPath);

        DateTime dateTimeOffsetNow = DateTime.UtcNow;
        if (cacheStampFileInfo.Exists &&
            cacheStampFileInfo.LastWriteTimeUtc.Add(MaxAcceptedCacheExpireTime) >= dateTimeOffsetNow &&
            ReadFromFile(cacheStampPath, out UrlStatus cachedStatus))
        {
            return cachedStatus;
        }

        using HttpResponseMessage response =
            await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url),
                                   HttpCompletionOption.ResponseHeadersRead,
                                   token)
                        .ConfigureAwait(false);

        UrlStatus status = new(response);
        WriteToFile(cacheStampPath, in status);
        return status;

        static unsafe void WriteToFile(string filePath, in UrlStatus urlStatus)
        {
            fixed (void* buffer = &urlStatus)
            {
                ReadOnlySpan<byte> bufferSpan = new(buffer, sizeof(UrlStatus));
                using FileStream fileStream = File.Open(filePath,
                                                        FileMode.Create,
                                                        FileAccess.ReadWrite,
                                                        FileShare.ReadWrite);
                fileStream.Write(bufferSpan);
            }
        }

        static unsafe bool ReadFromFile(string filePath, out UrlStatus urlStatus)
        {
            using FileStream fileStream = File.Open(filePath,
                                                    FileMode.Open,
                                                    FileAccess.ReadWrite,
                                                    FileShare.ReadWrite);
            urlStatus = default;
            Span<byte> urlStatusBuffer = new(Unsafe.AsPointer(ref urlStatus), sizeof(UrlStatus));
            int        read            = fileStream.Read(urlStatusBuffer);

            return read == sizeof(UrlStatus);
        }
    }

    /// <summary>
    /// This method is used to perform a cache cleanup up of the current cache directory based on <see cref="MaxAcceptedCacheExpireTime"/>.<br/>
    /// It's kind of expensive but this requires synchronous operation to make sure that the cache directory is cleaned up properly and avoid any possible race-condition issue.<br/>
    /// </summary>
    /// <param name="cachePath">The cache path to clean-up.</param>
    /// <param name="forceClean">Remove all caches even though the file isn't expired yet.</param>
    /// <returns>The cache path to use.</returns>
    public string? PerformCacheGarbageCollection(string? cachePath, bool forceClean = false)
    {
        if (string.IsNullOrEmpty(cachePath))
        {
            return cachePath;
        }

        if (cachePath.StartsWith(SkipGCPrefix))
        {
            return cachePath[SkipGCPrefix.Length..];
        }

        DirectoryInfo directoryInfo = new(cachePath);
        if (!directoryInfo.Exists)
        {
            return cachePath;
        }

        DateTime dateTimeOffsetNow = DateTime.UtcNow;
        foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
        {
            DateTime lastModifiedUtc    = fileInfo.LastWriteTimeUtc;
            TimeSpan remainedTimeOffset = dateTimeOffsetNow.Subtract(lastModifiedUtc);
            if (remainedTimeOffset <= MaxAcceptedCacheExpireTime && !forceClean)
            {
                continue;
            }

            try
            {
                fileInfo.Delete();
                Logger?.LogTrace("Removed cache file from last write {Date}: {FileName}", lastModifiedUtc, fileInfo.FullName);
            }
            catch (Exception e)
            {
                Logger?.LogError(e, "Cannot clean-up cache file: {FileName}", fileInfo.FullName);
            }
        }

        return cachePath;
    }

    /// <summary>
    /// Gets cached JSON response from a URL using a specified HTTP methods.
    /// </summary>
    /// <typeparam name="T">The type of instance to be deserialized.</typeparam>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="url">URL source for the response.</param>
    /// <param name="jsonTypeInfo">The JSON Type Info for deserializing JSON response to the target instance type.</param>
    /// <param name="httpMethod">HTTP Method used for getting the response.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The deserialized response from the JSON.</returns>
    public async Task<T?> GetFromCachedJsonAsync<T>(
        HttpClient        client,
        string?           url,
        JsonTypeInfo<T?>  jsonTypeInfo,
        HttpMethod?       httpMethod = null,
        CancellationToken token      = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        using HttpRequestMessage requestMessage = new(httpMethod ?? HttpMethod.Get, url);
        return await GetFromCachedJsonAsync(client, requestMessage, jsonTypeInfo, token);
    }

    /// <summary>
    /// Gets cached JSON response from a URL using a specified HTTP request message.
    /// </summary>
    /// <typeparam name="T">The type of instance to be deserialized.</typeparam>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="requestMessage">The request message to be sent to the server.</param>
    /// <param name="jsonTypeInfo">The JSON Type Info for deserializing JSON response to the target instance type.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The deserialized response from the JSON.</returns>
    public async Task<T?> GetFromCachedJsonAsync<T>(
        HttpClient         client,
        HttpRequestMessage requestMessage,
        JsonTypeInfo<T?>   jsonTypeInfo,
        CancellationToken  token = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(requestMessage);

        CDNCacheResult     result         = await TryGetCachedStreamFrom(client, requestMessage, token);
        await using Stream responseStream = result.Stream;
        return await JsonSerializer.DeserializeAsync(responseStream, jsonTypeInfo, token);
    }

    /// <summary>
    /// Gets cached string response using a specified HTTP methods.
    /// </summary>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="url">URL source for the response.</param>
    /// <param name="httpMethod">HTTP Method used for getting the response.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The response as a string.</returns>
    public async Task<string> GetFromCachedStringAsync(
        HttpClient        client,
        string?           url,
        HttpMethod?       httpMethod = null,
        CancellationToken token      = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        using HttpRequestMessage requestMessage = new(httpMethod ?? HttpMethod.Get, url);
        return await GetFromCachedStringAsync(client, requestMessage, token);
    }

    /// <summary>
    /// Gets cached string response using a specified HTTP request message.
    /// </summary>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="requestMessage">The request message to be sent to the server.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The response as a string.</returns>
    public async Task<string> GetFromCachedStringAsync(
        HttpClient         client,
        HttpRequestMessage requestMessage,
        CancellationToken  token = default)
    {
        ArgumentNullException.ThrowIfNull(requestMessage);

        CDNCacheResult     result         = await TryGetCachedStreamFrom(client, requestMessage, token);
        await using Stream responseStream = result.Stream;
        using StreamReader responseReader = new(responseStream);
        return await responseReader.ReadToEndAsync(token);
    }

    /// <summary>
    /// Gets cached response as a Stream using a specified HTTP methods.
    /// </summary>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="url">URL source for the response.</param>
    /// <param name="httpMethod">HTTP Method used for getting the response.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The response as an instance of a Stream.</returns>
    public async ValueTask<CDNCacheResult> TryGetCachedStreamFrom(
        HttpClient        client,
        string            url,
        HttpMethod?       httpMethod = null,
        CancellationToken token      = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        using HttpRequestMessage requestMessage = new(httpMethod ?? HttpMethod.Get, url);
        return await TryGetCachedStreamFrom(client, requestMessage, token);
    }

    /// <summary>
    /// Gets cached response as a Stream using a specified HTTP request message.
    /// </summary>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="requestMessage">The request message to be sent to the server.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The response as an instance of a Stream.</returns>
    public async ValueTask<CDNCacheResult> TryGetCachedStreamFrom(
        HttpClient         client,
        HttpRequestMessage requestMessage,
        CancellationToken  token = default)
    {
        bool                 isDispose  = false;
        HttpResponseMessage? response   = null;
        string               requestUrl = requestMessage.RequestUri?.OriginalString ?? "";

        if (!IsEnabled)
        {
            BridgedNetworkStream responseStream = await BridgedNetworkStream.CreateStream(client, requestMessage, token);
            return new CDNCacheResult
            {
                StatusCode = responseStream.StatusCode,
                Stream     = responseStream
            };
        }

        if (string.IsNullOrEmpty(CurrentCacheDir))
        {
            response = await client.SendAsync(requestMessage, token);
            return new CDNCacheResult
            {
                StatusCode = response.StatusCode,
                Stream     = await BridgedNetworkStream.CreateStream(response, token)
            };
        }

        string cacheDir = CurrentCacheDir;

        try
        {
            bool isAggressiveMode = IsUseAggressiveMode;

            if (!IsEnabled)
            {
                response = await client.SendAsync(requestMessage, token);
                return new CDNCacheResult
                {
                    StatusCode = response.StatusCode,
                    Stream     = await BridgedNetworkStream.CreateStream(client, requestMessage, token)
                };
            }

            if (!isAggressiveMode)
            {
                response = await client.SendAsync(requestMessage,
                                                  HttpCompletionOption.ResponseHeadersRead,
                                                  token);
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"URL: {requestUrl} returns a non-successful status code! {response.StatusCode} ({(int)response.StatusCode})",
                                                   null,
                                                   response.StatusCode);
                }

                return await TryGetCachedStreamFrom(response, token);
            }

            string hashString = GetXxh3HashFrom(requestUrl.AsSpan());
            if (TryCreateResultFromTimeCached(cacheDir, hashString) is { IsCached: true } result)
            {
                return result;
            }

            response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"URL: {requestUrl} returns a non-successful status code while using aggressive cache mode! {response.StatusCode} ({(int)response.StatusCode})",
                                               null,
                                               response.StatusCode);
            }

            DateTimeOffset nextExpireOffset = DateTimeOffset.UtcNow.Add(MaxAcceptedCacheExpireTime);
            Stream fileStream = AttachETag(hashString)
                ? CreateCacheStream(Path.Combine(cacheDir, hashString),
                                    cacheDir,
                                    hashString,
                                    await BridgedNetworkStream.CreateStream(response, token),
                                    true,
                                    nextExpireOffset)
                : await BridgedNetworkStream.CreateStream(response, token);

            return new CDNCacheResult
            {
                LocalCachePath     = Path.Combine(cacheDir, hashString),
                CacheETag          = hashString,
                CacheExpireTimeUtc = nextExpireOffset,
                Stream             = fileStream,
                StatusCode         = HttpStatusCode.OK
            };
        }
        catch
        {
            isDispose = true;
            throw;
        }
        finally
        {
            if (isDispose)
            {
                requestMessage.Dispose();
                response?.Dispose();
            }
        }
    }

    /// <summary>
    /// Gets cached response as a Stream using an existing HTTP response message.
    /// </summary>
    /// <param name="response">An existing HTTP response message.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The response as an instance of a Stream.</returns>
    public async ValueTask<CDNCacheResult> TryGetCachedStreamFrom(
        HttpResponseMessage? response,
        CancellationToken    token = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!IsEnabled)
        {
            BridgedNetworkStream stream = await BridgedNetworkStream.CreateStream(response, token);
            return new CDNCacheResult
            {
                StatusCode = stream.StatusCode,
                Stream     = stream
            };
        }

        string  cacheDir  = CurrentCacheDir!;
        bool    isDispose = false;
        string? etag      = null;

        try
        {
            if (TryGetETagBasedCacheType(response,
                                         out etag,
                                         out HashCacheType hashCacheType) &&
                await TryCreateResultFromETagCached(cacheDir,
                                                    etag,
                                                    response.Content.Headers.ContentLength ?? 0,
                                                    hashCacheType,
                                                    token) is { IsCached: true } resultFromETagBased)
            {
                response.Dispose();
                return resultFromETagBased;
            }

            bool isTimeBasedCache;
            if ((isTimeBasedCache = TryGetTimeBasedCacheType(response,
                                                             out etag,
                                                             out DateTimeOffset nextExpireTime)) &&
                TryCreateResultFromTimeCached(cacheDir,
                                              etag) is { IsCached: true } resultFromTimeBased)
            {
                response.Dispose();
                return resultFromTimeBased;
            }

            // Create HTTP Stream from the response.
            Stream returnStream;
            BridgedNetworkStream bridgedNetworkStream = await BridgedNetworkStream.CreateStream(response, token);

            // If the same etag is currently written, return network stream.
            if (string.IsNullOrEmpty(etag) || !AttachETag(etag))
            {
                return new CDNCacheResult
                {
                    StatusCode = bridgedNetworkStream.StatusCode,
                    Stream     = bridgedNetworkStream
                };
            }

            // Perform write-out if the cache is time-based.
            if (isTimeBasedCache)
            {
                // Start write cache based on its etag
                returnStream = CreateCacheStream(Path.Combine(cacheDir, etag),
                                                 cacheDir,
                                                 etag,
                                                 bridgedNetworkStream,
                                                 true,
                                                 nextExpireTime);
            }
            // Otherwise, use etag-based.
            else
            {
                // Check whether the cache is from ETag source.
                bool isAllowWriteToETagCache = hashCacheType != HashCacheType.None;

                // Start write cache based on its etag
                returnStream = isAllowWriteToETagCache
                    ? CreateCacheStream(Path.Combine(cacheDir, etag),
                                        cacheDir,
                                        etag,
                                        bridgedNetworkStream)
                    : bridgedNetworkStream;
            }

            return new CDNCacheResult
            {
                LocalCachePath     = string.IsNullOrEmpty(etag) ? null : Path.Combine(cacheDir, etag),
                CacheETag          = etag,
                CacheExpireTimeUtc = nextExpireTime,
                Stream             = returnStream,
                StatusCode         = HttpStatusCode.OK
            };
        }
        catch
        {
            isDispose = true;
            throw;
        }
        finally
        {
            if (isDispose)
            {
                response.Dispose();

                if (etag != null)
                {
                    DetachETag(etag);
                }
            }
        }
    }

    private CopyToStream CreateCacheStream(string cachePath,
                                           string cacheDir,
                                           string hashName,
                                           Stream source,
                                           bool isTimeBasedCache = false,
                                           DateTimeOffset nextExpireTime = default)
    {
        string tempPath = cachePath + ".temp";

        Directory.CreateDirectory(cacheDir);
        return new CopyToStream(source,
                                File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
                                null,
                                OnDispose,
                                true);

        void OnDispose()
        {
            if (!IsETagAttached(hashName))
            {
                return;
            }

            DetachETag(hashName);
            try
            {
                if (!File.Exists(tempPath))
                {
                    return;
                }

                File.Move(tempPath, cachePath, true);
                if (!isTimeBasedCache)
                {
                    return;
                }

                ReadOnlySpan<byte> stampBytes = CreateReadOnlySpanFrom(in nextExpireTime);
                string             stampPath  = Path.Combine(cacheDir, hashName + ".stamp");
                File.WriteAllBytes(stampPath, stampBytes);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error has occurred while renaming written cache file on path: {cachePath}\r\n{ex}", cachePath, ex);
            }
        }
    }

    private static unsafe ReadOnlySpan<byte> CreateReadOnlySpanFrom<T>(scoped in T data)
        where T : unmanaged
    {
        fixed (void* dataP = &data)
        {
            int sizeOf = sizeof(T);
            return new ReadOnlySpan<byte>(dataP, sizeOf);
        }
    }

    private static async ValueTask<CDNCacheResult?> TryCreateResultFromETagCached(
        string            cacheDir,
        [NotNull] string? etag,
        long              expectedSize,
        HashCacheType     hashCacheType,
        CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrEmpty(etag);

        string cachedFilePath = Path.Combine(cacheDir, etag);
        if (File.Exists(cachedFilePath) &&
            await TryCheckAndGetCachedStreamOrCreate(cachedFilePath,
                                                     etag,
                                                     expectedSize,
                                                     hashCacheType,
                                                     token) is { } cachedStream)
        {
            return new CDNCacheResult
            {
                IsCached       = true,
                LocalCachePath = cachedFilePath,
                CacheETag      = etag,
                Stream         = cachedStream,
                StatusCode     = HttpStatusCode.OK
            };
        }

        return null;
    }

    private static async ValueTask<Stream?> TryCheckAndGetCachedStreamOrCreate(
        string            cachedFilePath,
        string            etag,
        long              expectedSize,
        HashCacheType     hashCacheType,
        CancellationToken token)
    {
        FileStream? fileStream = null;
        byte[] hashOutput = ArrayPool<byte>.Shared.Rent(MaxHashLengthInBytes);
        bool isMatched = false;

        etag = etag.ToLower();

        try
        {
            bool isValidHex = HexTool.TryHexToBytesUnsafe(etag,
                                                          hashOutput,
                                                          out _,
                                                          out int actualHashLen) == OperationStatus.Done;
            if (!isValidHex)
            {
                return null;
            }

            if (hashCacheType.HasFlag(HashCacheType.CryptoType))
            {
                // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
                using HashAlgorithm hashAlgorithm = hashCacheType switch
                {
                    HashCacheType.MD5 => MD5.Create(),
                    HashCacheType.Sha1 => SHA1.Create(),
                    HashCacheType.Sha256 => SHA256.Create(),
                    _ => throw new NotSupportedException($"Crypto Hash Type: {hashCacheType} isn't supported!")
                };

                // >> 3 is equal to divided by 8. HashSize must be calculated since it returns bits, not bytes.
                if (hashAlgorithm.HashSize >> 3 != actualHashLen)
                {
                    return null;
                }

                fileStream = File.Open(cachedFilePath,
                                       FileMode.Open,
                                       FileAccess.Read,
                                       FileShare.ReadWrite);
                isMatched = await CheckHash(fileStream,
                                            hashAlgorithm,
                                            hashOutput.AsMemory(0, actualHashLen),
                                            token);
                fileStream.Position = 0;
            }

            if (isMatched)
            {
                return isMatched ? fileStream : null;
            }

            if (!hashCacheType.HasFlag(HashCacheType.CryptoType))
            {
                // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
                NonCryptographicHashAlgorithm hashAlgorithm = hashCacheType switch
                {
                    HashCacheType.Crc32 => new Crc32(),
                    HashCacheType.Crc64 => new Crc64(),
                    _ => throw new NotSupportedException($"Hash Type: {hashCacheType} isn't supported!")
                };

                if (hashAlgorithm.HashLengthInBytes != actualHashLen)
                {
                    return null;
                }

                fileStream = File.Open(cachedFilePath,
                                       FileMode.Open,
                                       FileAccess.Read,
                                       FileShare.ReadWrite);
                isMatched = await CheckHash(fileStream,
                                            hashAlgorithm,
                                            hashOutput.AsMemory(0, actualHashLen),
                                            token);
                fileStream.Position = 0;
            }

            if (!isMatched && expectedSize != 0 && fileStream?.Length == expectedSize)
            {
                isMatched = true;
            }

            return isMatched ? fileStream : null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(hashOutput);

            if (!isMatched && fileStream != null)
            {
                await fileStream.DisposeAsync();
            }
        }
    }

    private static string GetXxh3HashFrom<T>(ReadOnlySpan<T> span)
        where T : struct
    {
        Span<byte> hashBuffer = stackalloc byte[8];
        Span<char> hashChar   = stackalloc char[16];

        XxHash3.TryHash(MemoryMarshal.AsBytes(span), hashBuffer, out _);
        Convert.TryToHexStringLower(hashBuffer, hashChar, out _);

        return new string(hashChar);
    }

    private static async ValueTask<bool> CheckHash(
        FileStream        inputStream,
        HashAlgorithm     hashAlgorithm,
        Memory<byte>      hashToCheck,
        CancellationToken token)
    {
        byte[] dataBuffer = ArrayPool<byte>.Shared.Rent(8 << 10);

        try
        {
            int read;
            while ((read = await inputStream.ReadAsync(dataBuffer, token)) > 0)
            {
                hashAlgorithm.TransformBlock(dataBuffer, 0, read, dataBuffer, 0);
            }

            _ = hashAlgorithm.TransformFinalBlock(dataBuffer, 0, read);
            byte[] hashResult = hashAlgorithm.Hash ?? [];
            if (hashToCheck.Span.SequenceEqual(hashResult))
            {
                return true;
            }

            // Reverse, just in-case that the tag is actually Big-Endian. Try to compare again and return.
            Array.Reverse(hashResult);
            return hashToCheck.Span.SequenceEqual(hashResult);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(dataBuffer);
        }
    }

    private static async ValueTask<bool> CheckHash(
        FileStream                    inputStream,
        NonCryptographicHashAlgorithm hashAlgorithm,
        Memory<byte>                  hashToCheck,
        CancellationToken             token)
    {
        byte[] dataBuffer = ArrayPool<byte>.Shared.Rent(8 << 10);

        try
        {
            int read;
            while ((read = await inputStream.ReadAsync(dataBuffer, token)) > 0)
            {
                hashAlgorithm.Append(dataBuffer.AsSpan(0, read));
            }

            byte[] hashResult = hashAlgorithm.GetHashAndReset();
            if (hashToCheck.Span.SequenceEqual(hashResult))
            {
                return true;
            }

            // Reverse, just in-case that the tag is actually Big-Endian. Try to compare again and return.
            Array.Reverse(hashResult);
            return hashToCheck.Span.SequenceEqual(hashResult);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(dataBuffer);
        }
    }

    private bool TryGetTimeBasedCacheType(
        HttpResponseMessage                    response,
        [NotNullWhen(true)] out string?        requestHash,
        out                     DateTimeOffset nextExpireTime)
    {
        DateTimeOffset dateNowUtc            = DateTimeOffset.UtcNow;
        DateTimeOffset dateNowAdvancedMaxUtc = dateNowUtc.Add(MaxAcceptedCacheExpireTime);
        DateTimeOffset dateExpireFrom        = response.Content.Headers.Expires?.ToUniversalTime() ?? default;

        // Clamp if the CDN one is larger than the allowed max expire time.
        if (dateExpireFrom > dateNowAdvancedMaxUtc)
        {
            dateExpireFrom = dateNowAdvancedMaxUtc;
        }

        requestHash    = null;
        nextExpireTime = default;

        if (dateExpireFrom < dateNowUtc)
        {
            return false;
        }

        string? requestUrl = response.RequestMessage?.RequestUri?.ToString();
        if (string.IsNullOrEmpty(requestUrl))
        {
            return false;
        }

        nextExpireTime = dateExpireFrom;
        requestHash    = GetXxh3HashFrom(requestUrl.AsSpan());
        return true;
    }

    /// <summary>
    /// This method is used to sanitize trailing symbols embedded into the ETag string.
    /// Some CDN (example: Cloudflare) might use ""abcdef0123-2"" as its ETag string.
    /// 
    /// This method is used to sanitize and trim it into just a valid part of the string: abcdef0123
    /// </summary>
    private ReadOnlySpan<char> SanitizeHashCharSpan(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return span;
        }

        int startIndexOfSymbols = span.IndexOfAnyExcept(LetterNumberOnlyAsciiSearchValue);
        int startIndexOfLetters = span.IndexOfAnyExcept(SymbolOnlyAsciiSearchValue);
        int startBoundary       = Math.Max(startIndexOfSymbols, startIndexOfLetters);

        ReadOnlySpan<char> startIndexTrimmed = span[startBoundary..];
        int                endBoundary       = startIndexTrimmed.IndexOfAny(SymbolOnlyAsciiSearchValue);

        if (startBoundary >= 0 && endBoundary >= 0)
        {
            return span.Slice(startBoundary, endBoundary);
        }

        return span;
    }

    private bool TryGetETagBasedCacheType(
        HttpResponseMessage                   response,
        [NotNullWhen(true)] out string?       etag,
        out                     HashCacheType hashCacheType)
    {
        const int md5HashSize = MD5.HashSizeInBytes;

        hashCacheType = HashCacheType.None;
        etag = null;

        if (response.Content.Headers.TryGetValues("content-md5", out IEnumerable<string>? contentMd5Enum))
        {
            ReadOnlySpan<char> firstETag = contentMd5Enum.FirstOrDefault();
            firstETag = SanitizeHashCharSpan(firstETag);

            if (firstETag.IsEmpty)
            {
                return false;
            }

            Span<char> hashString = stackalloc char[md5HashSize * 2];
            Span<byte> hashBuffer = stackalloc byte[md5HashSize];

            // Try to decode them from Hex or Base64. Some providers might favor to use Base64 instead of Hex.
            if (HexTool.TryHexToBytesUnsafe(firstETag, hashBuffer, out _, out int hashBufferDecodedLen) != OperationStatus.Done ||
                !Convert.TryFromBase64Chars(firstETag, hashBuffer, out hashBufferDecodedLen))
            {
                return false;
            }

            if (hashBufferDecodedLen != md5HashSize ||
                !HexTool.TryBytesToHexUnsafe(hashBuffer, hashString))
            {
                return false;
            }

            hashCacheType = HashCacheType.MD5;
            etag          = new string(hashString);

            return true;
        }

        if (response.Headers.ETag is not { } etagProperty)
        {
            return false;
        }

        ReadOnlySpan<char> etagSpan = SanitizeHashCharSpan(etagProperty.Tag);
        if (etagSpan.IsEmpty || etagSpan.Length % 2 != 0)
        {
            return false;
        }

        int etagSizeInBytes = etagSpan.Length / 2;
        hashCacheType = etagSizeInBytes switch
                        {
                            32 => HashCacheType.Sha256,
                            20 => HashCacheType.Sha1,
                            16 => HashCacheType.MD5,
                            8 => HashCacheType.Crc64,
                            4 => HashCacheType.Crc32,
                            _ => HashCacheType.None
                        };

        if (hashCacheType == HashCacheType.None)
        {
            return false;
        }

        Span<byte> stackBuffer = stackalloc byte[etagSizeInBytes];
        if (HexTool.TryHexToBytesUnsafe(etagSpan, stackBuffer, out _, out _) != OperationStatus.Done)
        {
            return false;
        }

        Span<char> loweredETag = stackalloc char[etagSpan.Length];
        int        written     = etagSpan.ToLower(loweredETag, null);

        etag = loweredETag[..written].ToString();
        return true;
    }

    private static CDNCacheResult? TryCreateResultFromTimeCached(string cacheDir, string? requestHash)
    {
        if (string.IsNullOrEmpty(requestHash))
        {
            return null;
        }

        string         cacheFilePath         = Path.Combine(cacheDir, requestHash);
        string         cacheStampPath        = cacheFilePath + ".stamp";
        DateTimeOffset currentDateStamp      = default;
        bool           isFileStampExist      = File.Exists(cacheStampPath);
        bool           isFileExist           = File.Exists(cacheFilePath);
        bool           isReadAllBytesSuccess = TryReadAllBytes(cacheStampPath, GetAsSpan(ref currentDateStamp));

        if (!isFileStampExist || !isFileExist || !isReadAllBytesSuccess)
        {
            return null;
        }

        DateTimeOffset currentDateStampUtc = currentDateStamp.ToUniversalTime();
        if (currentDateStampUtc >= DateTimeOffset.UtcNow)
        {
            return new CDNCacheResult
            {
                IsCached           = true,
                LocalCachePath     = cacheFilePath,
                CacheExpireTimeUtc = currentDateStampUtc,
                Stream             = File.Open(cacheFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            };
        }

        return null;

        static unsafe Span<byte> GetAsSpan<T>(scoped ref T data) where T : unmanaged =>
            new(Unsafe.AsPointer(ref data), sizeof(T));

        static bool TryReadAllBytes(string path, Span<byte> buffer)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                _ = stream.Read(buffer);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private bool IsETagAttached(string? tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);

        using (ThisLock.EnterScope())
        {
            return CurrentETagToWrite.Contains(tag);
        }
    }

    private bool AttachETag(string? tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);

        using (ThisLock.EnterScope())
        {
            return CurrentETagToWrite.Add(tag);
        }
    }

    private void DetachETag(string? tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);

        using (ThisLock.EnterScope())
        {
            CurrentETagToWrite.Remove(tag);
        }
    }
}