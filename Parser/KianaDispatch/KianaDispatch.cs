using Hi3Helper.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

#nullable enable
namespace Hi3Helper.EncTool.Parser.KianaDispatch
{
    [JsonSerializable(typeof(KianaDispatch))]
    internal sealed partial class KianaDispatchContext : JsonSerializerContext;

    public class KianaDispatch
    {
        public static ILogger? DebugLogger { get; set; }

        #region Private Fields

        private string? _lastDispatchQuery;
        #endregion

        #region Properties
        [JsonPropertyName("dispatch_url")]
        public string? DispatchUrl { get; set; }

        [JsonPropertyName("name")]
        public string? DispatchCodename { get; set; }

        [JsonPropertyName("region_name")]
        public string? RegionCodename { get; set; }

        [JsonPropertyName("title")]
        public string? RegionTitle { get; set; }

        [JsonPropertyName("retcode")]
        public int ReturnCode { get; set; }

        [JsonPropertyName("is_data_ready")]
        public bool IsDataReady { get; set; }

        [JsonPropertyName("server_cur_time")]
        public ulong ServerCurrentTimeUtc { get; set; }

        [JsonPropertyName("server_cur_timezone")]
        public sbyte ServerCurrentTimeZone { get; set; }

        [JsonPropertyName("asset_bundle_url_list")]
        public string[] AssetBundleUrls { get; set; } = [];

        [JsonPropertyName("ex_resource_url_list")]
        public string[] ExternalAssetUrls { get; set; } = [];

        [JsonPropertyName("region_list")]
        public KianaDispatch[] Regions { get; set; } = [];

        // Added since v6.9 (nice) changes
        // :teri_copium:
        [JsonPropertyName("manifest")]
        public ManifestBase? Manifest { get; set; }
        #endregion

        public static ValueTask<KianaDispatch> GetDispatchAsync(
            HttpClient        client,
            string            dispatchBaseUrl,
            string            dispatchFormat,
            string            dispatchChannelName,
            string            baseKey,
            int[]             version,
            CancellationToken token = default)
        {
            if (version.Length != 3)
            {
                throw new ArgumentOutOfRangeException(nameof(version), version, "Version must consist of 3 numbers.");
            }

            // Format dispatch URL.
            string versionInString = $"{version[0]}.{version[1]}.{version[2]}";
            string dispatchQuery = string.Format(dispatchFormat, versionInString, dispatchChannelName, ConverterTool.GetUnixTimestamp(true));
            string urlEndpoint = dispatchBaseUrl + dispatchQuery;

            DebugLogger?.LogDebug("Connecting to Dispatch Server at: {url}", urlEndpoint);
            ValueTask<KianaDispatch> task =
                TryDeserializeJsonResponseFrom<KianaDispatch>(client,
                                                              urlEndpoint,
                                                              KianaDispatchContext.Default.KianaDispatch,
                                                              baseKey,
                                                              version,
                                                              token);

            task.GetAwaiter().OnCompleted(() =>
            {
                task.Result?._lastDispatchQuery = dispatchQuery;
            });

            return task;
        }

        public static async ValueTask<KianaDispatch> GetGameServerAsync(
            HttpClient        client,
            KianaDispatch     dispatch,
            string            regionName,
            string            baseKey,
            int[]             version,
            CancellationToken token = default)
        {
            // Find the correct region as per key from codename and select the first entry. If none, then return null (because .FirstOrDefault())
            // If the region results a null, then find a possible dispatch to read.
            KianaDispatch region = dispatch.Regions.FirstOrDefault(x => x.DispatchCodename == regionName)
                                   ?? await TryGetPossibleMatchingRegion(client, dispatch, token);

            string urlEndpoint = region.DispatchUrl + dispatch._lastDispatchQuery;
            DebugLogger?.LogDebug("Connecting to Game Region Server: {region} at: {url}", region.DispatchCodename, urlEndpoint);

            return await TryDeserializeJsonResponseFrom<KianaDispatch>(client,
                                                                       urlEndpoint,
                                                                       KianaDispatchContext.Default.KianaDispatch,
                                                                       baseKey,
                                                                       version,
                                                                       token);
        }

        private static async ValueTask<T> TryDeserializeJsonResponseFrom<T>(
            HttpClient        client,
            string            urlEndpoint,
            JsonTypeInfo<T>   jsonTypeInfo,
            string            baseKey,
            int[]             version,
            CancellationToken token = default)
        {
            const int maxBufferSize = 16 << 10; // 16 KB
            byte[] responseBuffer = ArrayPool<byte>.Shared.Rent(maxBufferSize);

            try
            {
                using MemoryStream rentBufferStream = new(responseBuffer, true);
                CDNCacheResult     httpRequest      = await client.TryGetCachedStreamFrom(urlEndpoint, null, token);
                if (!httpRequest.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(HttpRequestError.InvalidResponse,
                                                   $"Request to {urlEndpoint} failed with status code {httpRequest.StatusCode}",
                                                   statusCode: httpRequest.StatusCode);
                }

                await using Stream httpStream = httpRequest.Stream;
                await httpStream.CopyToAsync(rentBufferStream, token);

                return DeserializeWith(responseBuffer.AsSpan(0, (int)rentBufferStream.Position),
                                       jsonTypeInfo,
                                       baseKey,
                                       version)
                    ?? throw new InvalidOperationException("Cannot deserialize JSON response due to unknown reason.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(responseBuffer, true);
            }
        }

        private static unsafe T? DeserializeWith<T>(
            ReadOnlySpan<byte> underlyingBuffer,
            JsonTypeInfo<T>    jsonTypeInfo,
            string             baseKey,
            int[]              version)
        {
            // Trim the underlying buffer to remove any leading or trailing whitespace and newline characters.
            ReadOnlySpan<byte> bufferNotEncryptTest = underlyingBuffer.Trim(" \r\n"u8);

            // Try to check if the JSON response is not encrypted by checking if the first and last characters are '{' and '}' respectively.
            // If not encrypted, then directly deserialize.
            if (bufferNotEncryptTest[0] == '{' && bufferNotEncryptTest[^1] == '}')
            {
                return JsonSerializer.Deserialize(bufferNotEncryptTest, jsonTypeInfo);
            }

            // If the data is garbage, it's possibly encrypted, so... try to decrypt it.
            // -- Get decryption key
            if (!TryGetDecryptionKey(version, baseKey, out byte[] decryptKey))
            {
                throw new InvalidOperationException("Failed to generate decryption key.");
            }

            // -- UNSAFE: Get buffer pointer to assign it with UnmanagedMemoryStream.
            byte* bufferPtr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(underlyingBuffer));

            // -- Create AES instance and Transform
            using Aes aes = Aes.Create();
            aes.Key = decryptKey;
            aes.Mode = CipherMode.ECB;

            // -- Usually, encrypted data in every HoYo game's dispatch is encoded using Base64.
            //    So the flow would be like this:
            //    Data -> Decode from Base64 -> Decrypt -> Deserialized Data.
            using UnmanagedMemoryStream unmanagedMemoryStream = new(bufferPtr, underlyingBuffer.Length);
            using FromBase64Transform decodeTransform = new(FromBase64TransformMode.IgnoreWhiteSpaces);
            using ICryptoTransform decryptTransform = aes.CreateDecryptor();
            using CryptoStream decodeStream = new(unmanagedMemoryStream,
                                                  decodeTransform,
                                                  CryptoStreamMode.Read);
            using CryptoStream decryptStream = new(decodeStream,
                                                   decryptTransform,
                                                   CryptoStreamMode.Read);

            // -- Deserialize decrypted stream
            return JsonSerializer.Deserialize(decryptStream, jsonTypeInfo);
        }

        private static bool TryGetDecryptionKey(
            ReadOnlySpan<int>  version,
            ReadOnlySpan<char> baseKey,
            out byte[]         decryptionKey)
        {
            Unsafe.SkipInit(out decryptionKey);

            if (version.Length < 2)
            {
                return false;
            }

            // Concat the version and base key as merged key string.
            Span<byte> mergedKeyStringBuffer = stackalloc byte[16];
            int        offset                = 0;
            offset                          += FormatNumberToChar(mergedKeyStringBuffer, version[0]);
            mergedKeyStringBuffer[offset++] =  (byte)'.';
            offset                          += FormatNumberToChar(mergedKeyStringBuffer[offset..], version[1]);
            if (!Encoding.UTF8.TryGetBytes(baseKey, mergedKeyStringBuffer[offset..], out int written))
            {
                return false;
            }

            offset += written;
            mergedKeyStringBuffer = mergedKeyStringBuffer[..offset];

            // Generating the decryption key.
            // -- Phase 1: Hash the merged key bytes with MD5.
            Span<byte> phase1Buffer = stackalloc byte[16];
            if (!MD5.TryHashData(mergedKeyStringBuffer, phase1Buffer, out _))
            {
                return false;
            }

            // -- Phase 2: Convert the hash to a hex string and then to bytes.
            decryptionKey = new byte[32];
            return Convert.TryToHexStringLower(phase1Buffer, decryptionKey, out _);

            static int FormatNumberToChar<T>(Span<byte> buffer, T number)
                where T : IUtf8SpanFormattable
            {
                number.TryFormat(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, null);
                return bytesWritten;
            }
        }

        private static async ValueTask<KianaDispatch> TryGetPossibleMatchingRegion(HttpClient client, KianaDispatch dispatch, CancellationToken token)
        {
            // Do loop and find possible valid region/dispatch
            foreach (KianaDispatch region in dispatch.Regions)
            {
                string? regionDispatchUrl = region.DispatchUrl;

                if (string.IsNullOrEmpty(regionDispatchUrl))
                {
                    continue;
                }

                UrlStatus responseMessage = await client.GetCachedUrlStatus(regionDispatchUrl, token);
                if (responseMessage.IsSuccessStatusCode) return region;
            }

            // If not, then :terikms:
            throw new NullReferenceException("The valid dispatch/region does not exist!");
        }
    }
}
