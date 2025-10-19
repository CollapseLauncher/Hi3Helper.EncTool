using Hi3Helper.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CommentTypo
// ReSharper disable InconsistentNaming

#nullable enable
namespace Hi3Helper.EncTool;

/// <summary>
/// A record contains the result of the CDN cache.
/// </summary>
public record CDNCacheResult
{
    /// <summary>
    /// Whether the request is cached or not.<br/>
    /// </summary>
    public bool IsCached { get; set; }

    /// <summary>
    /// The local path of the cache file. This will be null if the request is not cached.<br/>
    /// </summary>
    public string? LocalCachePath { get; set; }

    /// <summary>
    /// The tag/hash of the cache file. This will be null if the request is not cached using hash-based cache (like ETag or Content-MD5).
    /// </summary>
    public string? CacheETag { get; set; }

    /// <summary>
    /// The expired time of the cache in UTC. This will return default value if the request is not cached using time-based cache.<br/>
    /// </summary>
    public DateTimeOffset CacheExpireTimeUtc { get; set; }

    /// <summary>
    /// The stream of the response content. This will never be null.<br/>
    /// A <see cref="CopyToStream"/> will be used if the response is actually determined to be cached. Otherwise, a <see cref="BridgedNetworkStream"/> will be used.<br/>
    /// </summary>
    public required Stream Stream { get; set; }
}

public static class CDNCacheUtil
{
    /// <summary>
    /// Gets/sets the current directory for storing the cache locally.<br/>
    /// While the cache directory is set, this will also perform a cache garbage collection based on <see cref="MaxAcceptedCacheExpireTime"/>.<br/>
    /// <br/>
    /// To skip the cache garbage collection, use <see cref="SetCacheDirSkipGC"/>.
    /// </summary>
    public static string? CurrentCacheDir
    {
        get;
        set => field = PerformCacheGarbageCollection(value);
    }

    /// <summary>
    /// Gets/sets the logger for this cache utility.
    /// </summary>
    public static ILogger? Logger { get; set; }

    /// <summary>
    /// Whether the CDN cache is enabled or not.
    /// </summary>
    public static bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether the aggressive mode is enabled or not (Default: disabled).<br/>
    /// This mode will try to cache all requests, even if the ETag or expire header is not present in the response.<br/>
    /// <br/>
    /// This method is valid only if <see cref="IsEnabled"/> is set to true and if <see cref="TryGetCachedStreamFrom(HttpClient,string,HttpMethod,System.Threading.CancellationToken)"/> method is used.<br/>
    /// </summary>
    public static bool IsUseAggressiveMode { get; set; } = false;

    /// <summary>
    /// Determine how long the maximum duration of the cache expire time is accepted (Default: 10 minutes).<br/>
    /// This will clamp the maximum duration of the cache expire time, even if the CDN provides a longer duration.<br/>
    /// <br/>
    /// This also sets how long the cache will be kept in the local directory before it is cleaned up, either it's hash-based or time-based cache.<br/>
    /// </summary>
    public static TimeSpan MaxAcceptedCacheExpireTime { get; set; } = TimeSpan.FromMinutes(10);

    #region Private Fields
    private static readonly Lock            ThisLock             = new();
    private static readonly HashSet<string> CurrentETagToWrite   = [];
    private const           int             MaxHashLengthInBytes = SHA256.HashSizeInBytes;

    private const string SkipGCPrefix = "Skip|";
    private const string LetterNumberOnlyAscii = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const string SymbolOnlyAscii = " !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
    private static readonly SearchValues<char> LetterNumberOnlyAsciiSearchValue = SearchValues.Create(LetterNumberOnlyAscii);
    private static readonly SearchValues<char> SymbolOnlyAsciiSearchValue = SearchValues.Create(SymbolOnlyAscii);
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
    public static void SetCacheDirSkipGC(string? cacheDir)
    {
        if (!string.IsNullOrEmpty(cacheDir))
        {
            cacheDir = SkipGCPrefix + cacheDir;
        }
        CurrentCacheDir = cacheDir;
    }

    /// <summary>
    /// This method is used to perform a cache cleanup up of the current cache directory based on <see cref="MaxAcceptedCacheExpireTime"/>.<br/>
    /// It's kind of expensive but this requires synchronous operation to make sure that the cache directory is cleaned up properly and avoid any possible race-condition issue.<br/>
    /// </summary>
    /// <param name="cachePath">The cache path to clean-up.</param>
    /// <param name="forceClean">Remove all caches even though the file isn't expired yet.</param>
    /// <returns>The cache path to use.</returns>
    public static string? PerformCacheGarbageCollection(string? cachePath, bool forceClean = false)
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

    public static async Task<T?> GetFromCachedJsonAsync<T>(
        this HttpClient   client,
        string?           url,
        JsonTypeInfo<T?>  jsonTypeInfo,
        HttpMethod?       httpMethod = null,
        CancellationToken token      = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        using HttpRequestMessage requestMessage = new HttpRequestMessage(httpMethod ?? HttpMethod.Get, url);
        return await client.GetFromCachedJsonAsync(requestMessage, jsonTypeInfo, token);
    }

    public static async Task<T?> GetFromCachedJsonAsync<T>(
        this HttpClient    client,
        HttpRequestMessage requestMessage,
        JsonTypeInfo<T?>   jsonTypeInfo,
        CancellationToken  token = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(requestMessage);

        CDNCacheResult     result         = await client.TryGetCachedStreamFrom(requestMessage, token);
        await using Stream responseStream = result.Stream;
        return await JsonSerializer.DeserializeAsync(responseStream, jsonTypeInfo, token);
    }

    public static async Task<string> GetFromCachedStringAsync(
        this HttpClient   client,
        string?           url,
        HttpMethod?       httpMethod = null,
        CancellationToken token      = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        using HttpRequestMessage requestMessage = new HttpRequestMessage(httpMethod ?? HttpMethod.Get, url);
        return await client.GetFromCachedStringAsync(requestMessage, token);
    }


    public static async Task<string> GetFromCachedStringAsync(
        this HttpClient    client,
        HttpRequestMessage requestMessage,
        CancellationToken  token = default)
    {
        ArgumentNullException.ThrowIfNull(requestMessage);

        CDNCacheResult     result         = await client.TryGetCachedStreamFrom(requestMessage, token);
        await using Stream responseStream = result.Stream;
        using StreamReader responseReader = new StreamReader(responseStream);
        return await responseReader.ReadToEndAsync(token);
    }

    public static async ValueTask<CDNCacheResult> TryGetCachedStreamFrom(
        this HttpClient   client,
        string            url,
        HttpMethod?       httpMethod = null,
        CancellationToken token      = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        using HttpRequestMessage requestMessage = new HttpRequestMessage(httpMethod ?? HttpMethod.Get, url);
        return await client.TryGetCachedStreamFrom(requestMessage, token);
    }

    public static async ValueTask<CDNCacheResult> TryGetCachedStreamFrom(
        this HttpClient    client,
        HttpRequestMessage requestMessage,
        CancellationToken  token = default)
    {
        bool                 isDispose = false;
        HttpResponseMessage? response  = null;

        string requestUrl = requestMessage.RequestUri?.OriginalString ?? "";

        if (!IsEnabled)
        {
            return new CDNCacheResult
            {
                Stream = await BridgedNetworkStream.CreateStream(client, requestMessage, token)
            };
        }

        if (string.IsNullOrEmpty(CurrentCacheDir))
        {
            response = await client.SendAsync(requestMessage, token);
            return new CDNCacheResult
            {
                Stream = await BridgedNetworkStream.CreateStream(response, token)
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
                    Stream = await BridgedNetworkStream.CreateStream(client, requestMessage, token)
                };
            }

            if (!isAggressiveMode)
            {
                response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, token);
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"URL: {requestUrl} returns a non-successful status code! {response.StatusCode} ({(int)response.StatusCode})",
                                                   null,
                                                   response.StatusCode);
                }

                return await response.TryGetCachedStreamFrom(token);
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
            Stream fileStream = AttachETag(hashString) ?
                CreateCacheStream(Path.Combine(cacheDir, hashString),
                                  cacheDir,
                                  hashString,
                                  await response.Content.ReadAsStreamAsync(token),
                                  true,
                                  nextExpireOffset) :
                await BridgedNetworkStream.CreateStream(response, token);

            return new CDNCacheResult
            {
                LocalCachePath     = Path.Combine(cacheDir, hashString),
                CacheETag          = hashString,
                CacheExpireTimeUtc = nextExpireOffset,
                Stream             = fileStream
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

    public static async ValueTask<CDNCacheResult> TryGetCachedStreamFrom(this HttpResponseMessage? response, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(response, nameof(response));

        if (!IsEnabled)
        {
            return new CDNCacheResult
            {
                Stream = await BridgedNetworkStream.CreateStream(response, token)
            };
        }

        string  cacheDir  = CurrentCacheDir!;
        bool    isDispose = false;
        string? etag      = null;

        try
        {
            if (TryGetETagBasedCacheType(response, out etag, out HashCacheType hashCacheType) &&
                await TryCreateResultFromETagCached(cacheDir, etag, response.Content.Headers.ContentLength ?? 0, hashCacheType, token) is { IsCached: true } resultFromETagBased)
            {
                response.Dispose();
                return resultFromETagBased;
            }

            bool isTimeBasedCache;
            if ((isTimeBasedCache = TryGetTimeBasedCacheType(response, out etag, out DateTimeOffset nextExpireTime)) &&
                TryCreateResultFromTimeCached(cacheDir, etag) is { IsCached: true } resultFromTimeBased)
            {
                response.Dispose();
                return resultFromTimeBased;
            }

            // Create HTTP Stream from the response.
            Stream               returnStream;
            BridgedNetworkStream bridgedNetworkStream = await BridgedNetworkStream.CreateStream(response, token);

            // If the same etag is currently written, return network stream.
            if (string.IsNullOrEmpty(etag) || !AttachETag(etag))
            {
                return new CDNCacheResult
                {
                    Stream = bridgedNetworkStream
                };
            }

            // Perform write-out if the cache is time-based.
            if (isTimeBasedCache)
            {
                // Start write cache based on its etag
                returnStream = CreateCacheStream(Path.Combine(cacheDir, etag), cacheDir, etag, bridgedNetworkStream, true, nextExpireTime);
            }
            // Otherwise, use etag-based.
            else
            {
                // Check whether the cache is from ETag source.
                bool isAllowWriteToETagCache = hashCacheType != HashCacheType.None;

                // Start write cache based on its etag
                returnStream = isAllowWriteToETagCache ?
                    CreateCacheStream(Path.Combine(cacheDir, etag), cacheDir, etag, bridgedNetworkStream) :
                    bridgedNetworkStream;
            }

            return new CDNCacheResult
            {
                LocalCachePath     = string.IsNullOrEmpty(etag) ? null : Path.Combine(cacheDir, etag),
                CacheETag          = etag,
                CacheExpireTimeUtc = nextExpireTime,
                Stream             = returnStream
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

    private static CopyToStream CreateCacheStream(string         cachePath,
                                                  string         cacheDir,
                                                  string         hashName,
                                                  Stream         source,
                                                  bool           isTimeBasedCache = false,
                                                  DateTimeOffset nextExpireTime   = default)
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
        ArgumentException.ThrowIfNullOrEmpty(etag, nameof(etag));

        string cachedFilePath = Path.Combine(cacheDir, etag);
        if (File.Exists(cachedFilePath) &&
            await TryCheckAndGetCachedStreamOrCreate(cachedFilePath, etag, expectedSize, hashCacheType, token) is { } cachedStream)
        {
            return new CDNCacheResult
            {
                IsCached       = true,
                LocalCachePath = cachedFilePath,
                CacheETag      = etag,
                Stream         = cachedStream
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
        byte[]      hashOutput = ArrayPool<byte>.Shared.Rent(MaxHashLengthInBytes);
        bool        isMatched  = false;

        etag = etag.ToLower();

        try
        {
            bool isValidHex = HexTool.TryHexToBytesUnsafe(etag, hashOutput, out _, out int actualHashLen) == OperationStatus.Done;
            if (!isValidHex)
            {
                return null;
            }

            if (hashCacheType.HasFlag(HashCacheType.CryptoType))
            {
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

                fileStream = File.Open(cachedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                isMatched = await CheckHash(fileStream, hashAlgorithm, hashOutput.AsMemory(0, actualHashLen), token);
                fileStream.Position = 0;
            }

            if (isMatched)
            {
                return isMatched ? fileStream : null;
            }

            if (!hashCacheType.HasFlag(HashCacheType.CryptoType))
            {
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

                fileStream = File.Open(cachedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                isMatched = await CheckHash(fileStream, hashAlgorithm, hashOutput.AsMemory(0, actualHashLen), token);
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

    private static async ValueTask<bool> CheckHash(FileStream inputStream, HashAlgorithm hashAlgorithm, Memory<byte> hashToCheck, CancellationToken token)
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

    private static async ValueTask<bool> CheckHash(FileStream inputStream, NonCryptographicHashAlgorithm hashAlgorithm, Memory<byte> hashToCheck, CancellationToken token)
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

    private static bool TryGetTimeBasedCacheType(HttpResponseMessage response, [NotNullWhen(true)] out string? requestHash, out DateTimeOffset nextExpireTime)
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
    private static ReadOnlySpan<char> SanitizeHashCharSpan(ReadOnlySpan<char> span)
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

    private static bool TryGetETagBasedCacheType(HttpResponseMessage response, [NotNullWhen(true)] out string? etag, out HashCacheType hashCacheType)
    {
        const int md5HashSize = MD5.HashSizeInBytes;

        hashCacheType = HashCacheType.None;
        etag          = null;

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
        int written = etagSpan.ToLower(loweredETag, null);

        etag = loweredETag[..written].ToString();
        return true;
    }

    private static CDNCacheResult? TryCreateResultFromTimeCached(string cacheDir, string? requestHash)
    {
        if (string.IsNullOrEmpty(requestHash))
        {
            return null;
        }

        string cacheFilePath  = Path.Combine(cacheDir, requestHash);
        string cacheStampPath = cacheFilePath + ".stamp";

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

        static unsafe Span<byte> GetAsSpan<T>(scoped ref T data)
            where T : unmanaged
        {
            void* ptr = Unsafe.AsPointer(ref data);
            int sizeOf = sizeof(T);

            return new Span<byte>(ptr, sizeOf);
        }

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

    private static bool IsETagAttached(string? tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag, nameof(tag));

        using (ThisLock.EnterScope())
        {
            return CurrentETagToWrite.Contains(tag);
        }
    }

    private static bool AttachETag(string? tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag, nameof(tag));

        using (ThisLock.EnterScope())
        {
            return CurrentETagToWrite.Add(tag);
        }
    }

    private static void DetachETag(string? tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag, nameof(tag));

        using (ThisLock.EnterScope())
        {
            CurrentETagToWrite.Remove(tag);
        }
    }
}

