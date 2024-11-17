using Hi3Helper.Data;
using Hi3Helper.Http;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

// Resharper disable all

namespace Hi3Helper.EncTool.Parser.KianaDispatch
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(KianaDispatch))]
    internal sealed partial class KianaDispatchContext : JsonSerializerContext { }

    public class KianaDispatch
    {
        #region Private Fields
        private static          string _dispatchQuery;
        private static          string _dispatchUrl;
        private static          string _keyString;
        #endregion

        #region Properties
        [JsonPropertyName("dispatch_url")] public string DispatchUrl { get; set; }
        [JsonPropertyName("name")] public string DispatchCodename { get; set; }
        [JsonPropertyName("region_name")] public string RegionCodename { get; set; }
        [JsonPropertyName("title")] public string RegionTitle { get; set; }
        [JsonPropertyName("retcode")] public int ReturnCode { get; set; }
        [JsonPropertyName("is_data_ready")] public bool IsDataReady { get; set; }
        [JsonPropertyName("server_cur_time")] public ulong ServerCurrentTimeUTC { get; set; }
        [JsonPropertyName("server_cur_timezone")] public sbyte ServerCurrentTimeZone { get; set; }

        [JsonPropertyName("asset_bundle_url_list")] public string[] AssetBundleUrls { get; set; }
        [JsonPropertyName("ex_resource_url_list")] public string[] ExternalAssetUrls { get; set; }
        [JsonPropertyName("region_list")] public KianaDispatch[] Regions { get; set; }

        // Added since v6.9 (nice) changes
        // :teri_copium:
        [JsonPropertyName("manifest")] public ManifestBase Manifest { get; set; }
        #endregion

        public static async Task<KianaDispatch> GetDispatch(DownloadClient downloadClient, string dispatchUrl, string dispatchFormat, string dispatchChannelName, string baseKey, int[] ver, CancellationToken token)
        {
            // Format the dispatch URL and set it to this instance
            _dispatchQuery = string.Format(dispatchFormat, $"{ver[0]}.{ver[1]}.{ver[2]}", dispatchChannelName, ConverterTool.GetUnixTimestamp(true));
            _dispatchUrl = dispatchUrl + _dispatchQuery;

            // Concatenate the base key with the short version string
            _keyString = $"{ver[0]}.{ver[1]}{baseKey}";

            // Intialize HTTP client class and try start to parse the dispatch
            return await TryParseDispatch(downloadClient, _dispatchUrl, token);
        }

#nullable enable
        public static async ValueTask<KianaDispatch> GetGameserver(DownloadClient downloadClient, KianaDispatch dispatch, string regionName, CancellationToken token)
        {
            // Find the correct region as per key from codename and select the first entry. If none, then return null (because .FirstOrDefault())
            // If the region results a null, then find a possible dispatch to read.
            KianaDispatch region = dispatch.Regions.Where(x => x.DispatchCodename == regionName)?.FirstOrDefault()
                ?? await TryGetPossibleMatchingRegion(downloadClient, dispatch, token);

            // Format the gameserver URL and set it to this instance, then try parsing the gateway (gameserver)
            string gameServerUrl = region.DispatchUrl + _dispatchQuery;
            return await TryParseDispatch(downloadClient, gameServerUrl, token);
        }

        private static async ValueTask<KianaDispatch> TryGetPossibleMatchingRegion(DownloadClient downloadClient, KianaDispatch dispatch, CancellationToken token)
        {
            // Do loop and find a possible valid region/dispatch
            for (int i = 0; i < dispatch.Regions.Length; i++)
            {
                var region = dispatch.Regions[i];
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, new Uri(region.DispatchUrl));
                HttpResponseMessage responseMessage = await downloadClient.GetHttpClient().SendAsync(requestMessage, token);
                if (responseMessage.IsSuccessStatusCode) return region;
            }

            // If not, then :terikms:
            throw new NullReferenceException("The valid dispatch/region isn't exist!");
        }
#nullable disable

        private static async Task<KianaDispatch> TryParseDispatch(DownloadClient http, string dispatchUrl, CancellationToken token)
        {
            // Initialize memory stream
            using (Stream memStream = new MemoryStream())
            {
                // Start download the content and set the output to memory stream
                await http.DownloadAsync(dispatchUrl, memStream, true, cancelToken: token);

                // Check if the response is encrypted or not
                if (IsResponseEncrypted(memStream))
                {
                    // If it's encrypted, get the Base64 decoder stream, get the Decrypt stream and parse the response
                    using (Stream responseStream = GetTransformBase64Stream(memStream))
                    using (Stream cryptStream = GetCryptStream(responseStream))
                    {
#if DEBUG
                        string line;
                        using (StreamReader ln = new StreamReader(cryptStream))
                        {
                            line = ln.ReadLine();
                            Console.WriteLine($"Response {dispatchUrl}:\r\n{line}");
                            return (KianaDispatch)JsonSerializer.Deserialize(line, typeof(KianaDispatch), KianaDispatchContext.Default);
                        }
#else
                        return (KianaDispatch)JsonSerializer.Deserialize(cryptStream, typeof(KianaDispatch), KianaDispatchContext.Default);
#endif
                    }
                }

                // If not, then assume it's a JSON pure string and parse the response
                return (KianaDispatch)JsonSerializer.Deserialize(memStream, typeof(KianaDispatch), KianaDispatchContext.Default);
            }
        }

        private static bool IsResponseEncrypted(Stream source)
        {
            // Seek it to the beginning
            source.Seek(0, SeekOrigin.Begin);

            // Get the first byte to get the mark
            int startMark = source.ReadByte();
            // If the startMark is not 0x7b, then mark the response to be encrypted
            bool isEncrypt = startMark != 0x7b; // 0x7b is { mark
            // Back to the beginning
            source.Seek(0, SeekOrigin.Begin);

            // Return the value
            return isEncrypt;
        }

        private static Stream GetTransformBase64Stream(Stream source)
        {
            // Get the Base64 transform interface then return the stream
            ICryptoTransform base64Transform = new FromBase64Transform(FromBase64TransformMode.IgnoreWhiteSpaces);
            return new CryptoStream(source, base64Transform, CryptoStreamMode.Read);
        }

        private static Stream GetCryptStream(Stream inputStream)
        {
            // Get the AES transform interface then return the stream
            ICryptoTransform cryptTransform = GetAESTransform();
            return new CryptoStream(inputStream, cryptTransform, CryptoStreamMode.Read);
        }

        private static ICryptoTransform GetAESTransform()
        {
            // Create the MD5 and AES instance
            MD5 md5 = MD5.Create();
            Aes aes = Aes.Create();

            // Get the bytes of the phase 1 key string
            byte[] phase1KeyBytes = Encoding.UTF8.GetBytes(_keyString);
            // Compute the hash bytes of the phase 1 key
            // NOTE: At this moment, the byte array always be expected as 16 bytes wide for MD5 hash.
            Span<byte> phase1KeyHash = stackalloc byte[16];
            md5.TryComputeHash(phase1KeyBytes, phase1KeyHash, out int _);

            // Convert the phase 1 key hash to Hex string
            // NOTE: The result will always be a lowered case string
            string phase2Key = HexTool.BytesToHexUnsafe(phase1KeyHash);

            // Convert the hex string as phase 2 key bytes
            // NOTE: The phase 2 key array always be expected as 32 bytes wide as it means that
            //       the encryption used is 256-bit (32 * 8) wide (at this case, AES-256-ECB).
            byte[] phase2KeyBytes = new byte[32];
            Encoding.UTF8.GetBytes(phase2Key, phase2KeyBytes);

            // Assign the key to the AES instance and set the mode to ECB
            aes.Key = phase2KeyBytes;
            aes.Mode = CipherMode.ECB;

            // Create the decryptor and rerurn the transform interface
            return aes.CreateDecryptor();
        }
    }
}
