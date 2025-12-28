using Google.Protobuf;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Proto.StarRail;
using Hi3Helper.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

#if !DEBUG
using System.Security.Cryptography;
#endif

// ReSharper disable InconsistentNaming
// ReSharper disable PartialTypeWithSinglePart

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    internal class SRDispatchArchiveInfo
    {
        public uint   MajorVersion             { get; set; }
        public uint   MinorVersion             { get; set; }
        public uint   PatchVersion             { get; set; }
        public string ContentHash              { get; set; }
        public uint   FileSize                 { get; set; }
        public uint   TimeStamp                { get; set; }
        public string FileName                 { get; set; }
        public string BaseAssetsDownloadUrl    { get; set; }
        public string FullAssetsDownloadUrl    { get; set; }
        public string FullAssetsDownloadUrlAlt { get; set; }
    }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(SRDispatchArchiveInfo))]
    internal sealed partial class SRDispatchArchiveInfoContext : JsonSerializerContext;

    public sealed partial class SRDispatcherInfo : IDisposable
    {
        private  string                PersistentDirectory { get; set; }
        private  string                DispatchURLFormat   { get; }
        private  string                GatewayURLFormat    { get; }
        private  string                DispatchURL         { get; }
        private  string                DispatchSeed        { get; }
        private  string                ProductID           { get; }
        private  string                ProductVer          { get; }
        internal RegionInfo            RegionInfo          { get; set; }
        public   StarRailGatewayStatic RegionGateway       { get; set; }

        internal Dictionary<string, SRDispatchArchiveInfo>  ArchiveInfo { get; set; }

        public SRDispatcherInfo(string dispatchURL,
                                string dispatchSeed,
                                string dispatchFormatTemplate,
                                string gatewayFormatTemplate,
                                string productID,
                                string productVer)
        {
            DispatchURL       = dispatchURL;
            DispatchSeed      = dispatchSeed;
            ProductID         = productID;
            ProductVer        = productVer;
            DispatchURLFormat = dispatchFormatTemplate;
            GatewayURLFormat  = gatewayFormatTemplate;
        }

        public SRDispatcherInfo(List<string> dispatchUrls,
                                string       dispatchSeed,
                                string       dispatchFormatTemplate,
                                string       gatewayFormatTemplate,
                                string       productID,
                                string       productVer)
        {
            DispatchURL       = dispatchUrls[Random.Shared.Next(0, dispatchUrls.Count - 1)];
            DispatchSeed      = dispatchSeed;
            ProductID         = productID;
            ProductVer        = productVer;
            DispatchURLFormat = dispatchFormatTemplate;
            GatewayURLFormat  = gatewayFormatTemplate;
        }

        ~SRDispatcherInfo() => Dispose();

        public void Dispose()
        {
            ArchiveInfo?.Clear();
            RegionInfo = null;
            GC.SuppressFinalize(this);
        }

        internal async Task Initialize(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string persistentDirectory, string regionName, CancellationToken token = default)
        {
            PersistentDirectory = persistentDirectory;

            await ParseDispatch(downloadClient.GetHttpClient(), regionName, token);
            await ParseGateway(downloadClient.GetHttpClient(), token);
            await ParseArchive(downloadClient, downloadProgressDelegate, token);
        }

        public async Task Initialize(
            HttpClient        client,
            string            regionName,
            CancellationToken token = default)
        {
            await ParseDispatch(client, regionName, token);
            await ParseGateway(client, token);
        }

        private async Task ParseDispatch(HttpClient client, string regionName, CancellationToken token = default)
        {
            // Format dispatcher URL
            string dispatchURL = DispatchURL + string.Format(DispatchURLFormat, ProductID, ProductVer, SRMetadata.GetUnixTimestamp());

#if DEBUG
            Console.WriteLine($"Dispatch URL: {dispatchURL}");
#endif

            // Deserialize dispatcher and assign the region
            StarRailDispatch dispatch = await ParseFromUrlAsync(dispatchURL, client, StarRailDispatch.Parser, token);
            RegionInfo = dispatch.RegionList.FirstOrDefault(x => x.Name == regionName);

            if (RegionInfo == null) throw new KeyNotFoundException($"Region: {regionName} is not found in the dispatcher! Ignore this error if the game is under maintenance.");
        }

        private async Task ParseGateway(HttpClient client, CancellationToken token = default)
        {
            // Format dispatcher URL
            string gatewayURL = RegionInfo.DispatchUrl + string.Format(GatewayURLFormat, ProductID, ProductVer, SRMetadata.GetUnixTimestamp(), DispatchSeed);

#if DEBUG
            Console.WriteLine($"Gateway URL: {gatewayURL}");
#endif

            // Deserialize gateway
            RegionGateway = await ParseFromUrlAsync(gatewayURL, client, StarRailGatewayStatic.Parser, token);
        }

        private static async Task<T> ParseFromUrlAsync<T>(string            url,
                                                          HttpClient        client,
                                                          MessageParser<T>  parser,
                                                          CancellationToken token)
            where T : IMessage<T>
        {
            // Get the cached stream from the URL.
            await using Stream responseStream = (await client.TryGetCachedStreamFrom(url, token: token)).Stream;

#if DEBUG
            using StreamReader reader          = new(responseStream);
            string             responseContent = await reader.ReadToEndAsync(token);
            byte[]             content         = Convert.FromBase64String(responseContent);

            Console.WriteLine($"[SRDispatcherInfo::ParseFromUrlAsync<{typeof(T).Name}>()] Response (in Base64): {responseContent}");
#else
            using FromBase64Transform base64Transform = new FromBase64Transform(FromBase64TransformMode.IgnoreWhiteSpaces);
            await using CryptoStream  content = new CryptoStream(responseStream, base64Transform, CryptoStreamMode.Read);
#endif

            T parsedContent = parser.ParseFrom(content);
            return parsedContent;
        }

        private async Task ParseArchive(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken token = default)
        {
            /* ===============================
             * Archive_V region
             * ===============================
             */
            ArchiveInfo = new Dictionary<string, SRDispatchArchiveInfo>();
            string archiveURL = RegionGateway.ValuePairs["AssetBundleVersionUpdateUrl"] + "/client/Windows/Archive/M_ArchiveV.bytes";
            string localPath = Path.Combine(PersistentDirectory, @"Archive\Windows\M_ArchiveV_cache.bytes");

#if DEBUG
            Console.WriteLine("Parsing Archive -----");
            Console.WriteLine($"    URL: {archiveURL}");
            Console.WriteLine($"    LocalPath: {localPath}");
#endif

            // Parse M_ArchiveV first
            await DownloadAndParseArchiveInfo(downloadClient, downloadProgressDelegate, "AssetBundleVersionUpdateUrl", archiveURL, localPath);

            /* ===============================
             * Design_Archive_V region
             * ===============================
             */
            archiveURL = RegionGateway.ValuePairs["DesignDataBundleVersionUpdateUrl"] + "/client/Windows/M_Design_ArchiveV.bytes";

#if DEBUG
            Console.WriteLine("Parsing Design Archive -----");
            Console.WriteLine($"    URL: {archiveURL}");
#endif
            // Get HttpClient and response
            HttpClient client = downloadClient.GetHttpClient();
            // Parse design archive and get Native Data resources
            await using Stream designArchiveStream = (await client.TryGetCachedStreamFrom(archiveURL, token: token)).Stream;
            await ParseArchiveInfoFromStream(designArchiveStream, "DesignDataBundleVersionUpdateUrl", token);

            const string NativeDataRefDictKey  = "M_NativeDataV";
            const string NativeDataDataDictKey = "NativeDataV_";
            if (ArchiveInfo.TryGetValue(NativeDataRefDictKey, out SRDispatchArchiveInfo nativeDataArchiveInfo))
            {
                const string localNativeDataRefName = NativeDataRefDictKey + ".bytes";
                string       localNativeDataRefUrl  = nativeDataArchiveInfo.FullAssetsDownloadUrl.CombineURLFromString(localNativeDataRefName);
                string       localNativeDataRefPath = Path.Combine(PersistentDirectory, "NativeData\\Windows", localNativeDataRefName);
#if DEBUG
                Console.WriteLine($"    NativeDataRefUrl: {localNativeDataRefUrl}");
                Console.WriteLine($"    NativeDataRefPath: {localNativeDataRefPath}");
#endif
                await DownloadArchiveInfo(downloadClient, downloadProgressDelegate, localNativeDataRefUrl, localNativeDataRefPath);

                string localNativeDataResName = NativeDataDataDictKey + nativeDataArchiveInfo.ContentHash + ".bytes";
                string localNativeDataResUrl  = nativeDataArchiveInfo.FullAssetsDownloadUrl.CombineURLFromString(localNativeDataResName);
                string localNativeDataResPath = Path.Combine(PersistentDirectory, "NativeData\\Windows", localNativeDataResName);
#if DEBUG
                Console.WriteLine($"    NativeDataResUrl: {localNativeDataResUrl}");
                Console.WriteLine($"    NativeDataResPath: {localNativeDataResPath}");
#endif
                await DownloadArchiveInfo(downloadClient, downloadProgressDelegate, localNativeDataResUrl, localNativeDataResPath);
            }
        }

        private async Task DownloadArchiveInfo(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string archiveURL, string localPath, CancellationToken token = default)
        {
            EnsureDirectoryExistence(localPath);
            await using FileStream stream = new(localPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            await downloadClient.PerformCopyToDownload(archiveURL, downloadProgressDelegate, stream, token);
        }

        private async Task DownloadAndParseArchiveInfo(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string gatewayDictKey, string archiveURL, string localPath, CancellationToken token = default)
        {
            EnsureDirectoryExistence(localPath);
            await using FileStream stream = new(localPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            await downloadClient.PerformCopyToDownload(archiveURL, downloadProgressDelegate, stream, token);
            stream.Position = 0;
            await ParseArchiveInfoFromStream(stream, gatewayDictKey, token);
        }

        private static void EnsureDirectoryExistence(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private async Task ParseArchiveInfoFromStream(Stream stream, string gatewayDictKey, CancellationToken token)
        {
            using StreamReader reader = new(stream);
            while (await reader.ReadLineAsync(token) is { } line)
            {
                SRDispatchArchiveInfo archiveInfo = JsonSerializer.Deserialize(line, SRDispatchArchiveInfoContext.Default.SRDispatchArchiveInfo);
                string baseUrl = string.IsNullOrEmpty(archiveInfo.BaseAssetsDownloadUrl) ?
                    RegionGateway.ValuePairs[gatewayDictKey] :
                    TrimLastURLRelativePath(RegionGateway.ValuePairs[gatewayDictKey]);

                string relativePath = archiveInfo.FileName switch
                {
                    "M_AudioV" => "/client/Windows/AudioBlock",
                    "M_VideoV" => "/client/Windows/Video",
                    "M_DesignV" or "M_Design_PatchV" => "/client/Windows",
                    "M_NativeDataV" => "/client/Windows/NativeData",
                    _ => "/client/Windows/Block"
                };

                archiveInfo.FullAssetsDownloadUrl = 
                    baseUrl.CombineURLFromString(archiveInfo.BaseAssetsDownloadUrl,
                                                 relativePath);

                if (RegionGateway.ValuePairs.TryGetValue(gatewayDictKey + "Alt", out string baseUrlAlt))
                {
                    archiveInfo.FullAssetsDownloadUrlAlt =
                        baseUrlAlt.CombineURLFromString(archiveInfo.BaseAssetsDownloadUrl,
                                                        relativePath);
                }

                ArchiveInfo.Add(archiveInfo.FileName, archiveInfo);
            }
        }

        private static string TrimLastURLRelativePath(string url)
        {
            string[] urlPart = url.Split('/');

            string ret = string.Join('/', urlPart[..^1]);
            return ret;
        }
    }
}
