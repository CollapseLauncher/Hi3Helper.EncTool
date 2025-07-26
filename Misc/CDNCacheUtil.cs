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

#nullable enable
namespace Hi3Helper.EncTool;

public static class CDNCacheUtil
{
    public static string?  CurrentCacheDir { get; set; }
    public static ILogger? Logger          { get; set; }

    private static readonly Lock            ThisLock             = new();
    private static readonly HashSet<string> CurrentETagToWrite   = [];
    private const           int             MaxHashLengthInBytes = SHA256.HashSizeInBytes;

    private const string LetterNumberOnlyAscii = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const string SymbolOnlyAscii = " !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
    private static readonly SearchValues<char> LetterNumberOnlyAsciiSearchValue = SearchValues.Create(LetterNumberOnlyAscii);
    private static readonly SearchValues<char> SymbolOnlyAsciiSearchValue = SearchValues.Create(SymbolOnlyAscii);

    [Flags]
    private enum CacheTypeAssumption
    {
        None       = 0,
        CryptoType = 0b_00000001_00000000,

        Crc32  = 0b_00000000_00000001,
        Crc64  = 0b_00000000_00000010,
        MD5    = CryptoType | 0b_00000000_00000100,
        Sha1   = CryptoType | 0b_00000000_00001000,
        Sha256 = CryptoType | 0b_00000000_00010000
    }

    public record CDNCacheResult
    {
        public          bool    IsCached       { get; set; }
        public          string? LocalCachePath { get; set; }
        public          string? CacheETag      { get; set; }
        public required Stream  Stream         { get; set; }
    }

    public static async Task<T?> GetFromCachedJsonAsync<T>(this HttpClient client, string? url, JsonTypeInfo<T?> jsonTypeInfo, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        CDNCacheResult result = await client.TryGetCachedStreamFrom(url, null, cancellationToken);
        return await JsonSerializer.DeserializeAsync(result.Stream, jsonTypeInfo, cancellationToken);
    }

    public static async ValueTask<CDNCacheResult> TryGetCachedStreamFrom(this HttpResponseMessage? response, CancellationToken token = default)
    {
        string              cacheDir            = CurrentCacheDir!;
        bool                isDispose           = false;
        string?             etag                = null;
        CacheTypeAssumption cacheTypeAssumption = CacheTypeAssumption.None;

        try
        {
            ArgumentNullException.ThrowIfNull(response, nameof(response));

            bool isTimeBasedCache;
            if ((isTimeBasedCache = TryGetTimeBasedCacheType(response, out etag, out DateTimeOffset nextExpireTime)) &&
                TryCreateResultFromTimeCached(cacheDir, etag) is { IsCached: true } resultFromTimeBased)
            {
                return resultFromTimeBased;
            }

            if (!isTimeBasedCache &&
                TryGetETagBasedCacheType(response, out etag, out cacheTypeAssumption) &&
                await TryCreateResultFromETagCached(cacheDir, etag, response.Content.Headers.ContentLength ?? 0, cacheTypeAssumption, token) is { IsCached: true } resultFromETagBased)
            {
                return resultFromETagBased;
            }

            // Create HTTP Stream from the response.
            Stream               returnStream;
            BridgedNetworkStream bridgedNetworkStream = await BridgedNetworkStream.CreateStream(response, token);

            // If the same etag is currently written, return network stream.
            if (string.IsNullOrEmpty(etag) || !AttachETag(etag))
            {
                return new CDNCacheResult
                {
                    IsCached       = false,
                    LocalCachePath = null,
                    CacheETag      = null,
                    Stream         = bridgedNetworkStream
                };
            }

            // Perform write-out if the cache is time-based.
            if (isTimeBasedCache)
            {
                // Start write cache based on its etag
                returnStream = CreateCacheStreamFromETag(Path.Combine(cacheDir, etag), bridgedNetworkStream);
            }
            // Otherwise, use etag-based.
            else
            {
                // Check whether the cache is from ETag source.
                bool isAllowWriteToETagCache = cacheTypeAssumption != CacheTypeAssumption.None;

                // Start write cache based on its etag
                returnStream = isAllowWriteToETagCache ?
                    CreateCacheStreamFromETag(Path.Combine(cacheDir, etag), bridgedNetworkStream) :
                    bridgedNetworkStream;
            }

            return new CDNCacheResult
            {
                IsCached       = false,
                LocalCachePath = string.IsNullOrEmpty(etag) ? null : Path.Combine(cacheDir, etag),
                CacheETag      = etag,
                Stream         = returnStream
            };

            Stream CreateCacheStreamFromETag(string cachePath, Stream source)
            {
                string tempPath = cachePath + ".temp";

                Directory.CreateDirectory(cacheDir);
                return new CopyToStream(source,
                                        File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
                                        null,
                                        () =>
                                        {
                                            if (!IsETagAttached(etag))
                                            {
                                                return;
                                            }

                                            DetachETag(etag);
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
                                                string stampPath = Path.Combine(cacheDir, etag + ".stamp");
                                                File.WriteAllBytes(stampPath, stampBytes);
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger?.LogError(ex, "Error has occurred while renaming written cache file on path: {cachePath}\r\n{ex}", cachePath, ex);
                                            }
                                        },
                                        true);
            }
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
                response?.Dispose();

                if (etag != null)
                {
                    DetachETag(etag);
                }
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

    public static async ValueTask<CDNCacheResult> TryGetCachedStreamFrom(this HttpClient client, string url, HttpMethod? httpMethod = null, CancellationToken token = default)
    {
        bool isDispose = false;
        HttpRequestMessage? message = null;
        HttpResponseMessage? response = null;

        try
        {
            message = new HttpRequestMessage(httpMethod ?? HttpMethod.Get, url);
            response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"URL: {url} returns a non-successful status code! {response.StatusCode} ({(int)response.StatusCode})",
                    null,
                    response.StatusCode);
            }

            return await response.TryGetCachedStreamFrom(token);
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
                message?.Dispose();
                response?.Dispose();
            }
        }
    }

    private static async ValueTask<CDNCacheResult?> TryCreateResultFromETagCached(
        string cacheDir,
        [NotNull] string? etag,
        long expectedSize,
        CacheTypeAssumption cacheTypeAssumption,
        CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrEmpty(etag, nameof(etag));

        string cachedFilePath = Path.Combine(cacheDir, etag);
        if (File.Exists(cachedFilePath) &&
            await TryCheckAndGetCachedStreamOrCreate(cachedFilePath, etag, expectedSize, cacheTypeAssumption, token) is { } cachedStream)
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
        string cachedFilePath,
        string etag,
        long expectedSize,
        CacheTypeAssumption cacheTypeAssumption,
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

            if (cacheTypeAssumption.HasFlag(CacheTypeAssumption.CryptoType))
            {
                using HashAlgorithm hashAlgorithm = cacheTypeAssumption switch
                {
                    CacheTypeAssumption.MD5 => MD5.Create(),
                    CacheTypeAssumption.Sha1 => SHA1.Create(),
                    CacheTypeAssumption.Sha256 => SHA256.Create(),
                    _ => throw new NotSupportedException($"Crypto Hash Type: {cacheTypeAssumption} isn't supported!")
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

            if (!cacheTypeAssumption.HasFlag(CacheTypeAssumption.CryptoType))
            {
                NonCryptographicHashAlgorithm hashAlgorithm = cacheTypeAssumption switch
                {
                    CacheTypeAssumption.Crc32 => new Crc32(),
                    CacheTypeAssumption.Crc64 => new Crc64(),
                    _ => throw new NotSupportedException($"Hash Type: {cacheTypeAssumption} isn't supported!")
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
        DateTimeOffset dateExpireFrom = response.Content.Headers.Expires?.ToUniversalTime() ?? default;

        requestHash    = null;
        nextExpireTime = default;

        if (dateExpireFrom < DateTimeOffset.UtcNow)
        {
            return false;
        }

        string? requestUrl = response.RequestMessage?.RequestUri?.ToString();
        if (string.IsNullOrEmpty(requestUrl))
        {
            return false;
        }

        nextExpireTime = dateExpireFrom;
        Span<byte> hashBuffer = stackalloc byte[8];
        Span<char> hashChar   = stackalloc char[16];

        XxHash3.TryHash(MemoryMarshal.AsBytes(requestUrl.AsSpan()), hashBuffer, out _);
        Convert.TryToHexStringLower(hashBuffer, hashChar, out _);

        requestHash = new string(hashChar);
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

    private static bool TryGetETagBasedCacheType(HttpResponseMessage response, [NotNullWhen(true)] out string? etag, out CacheTypeAssumption cacheTypeAssumption)
    {
        const int md5HashSize = MD5.HashSizeInBytes;

        cacheTypeAssumption = CacheTypeAssumption.None;
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

            cacheTypeAssumption = CacheTypeAssumption.MD5;
            etag                = new string(hashString);

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
        cacheTypeAssumption = etagSizeInBytes switch
        {
            32 => CacheTypeAssumption.Sha256,
            20 => CacheTypeAssumption.Sha1,
            16 => CacheTypeAssumption.MD5,
            8 => CacheTypeAssumption.Crc64,
            4 => CacheTypeAssumption.Crc32,
            _ => CacheTypeAssumption.None
        };

        if (cacheTypeAssumption == CacheTypeAssumption.None)
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

        DateTimeOffset currentDateStamp = default;

        if (!File.Exists(cacheStampPath) ||
            !File.Exists(cacheFilePath) ||
            !TryReadAllBytes(cacheStampPath, GetAsSpan(ref currentDateStamp)))
        {
            return null;
        }

        DateTimeOffset currentDateStampUtc = currentDateStamp.ToUniversalTime();
        if (currentDateStampUtc <= DateTimeOffset.UtcNow)
        {
            return new CDNCacheResult
            {
                CacheETag = requestHash,
                IsCached = true,
                LocalCachePath = cacheFilePath,
                Stream = File.Open(cacheFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
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

