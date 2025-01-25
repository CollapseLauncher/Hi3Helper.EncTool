using Hi3Helper.Data;
using Hi3Helper.EncTool.Proto.StarRail;
using Hi3Helper.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable InconsistentNaming
// ReSharper disable PartialTypeWithSinglePart
#pragma warning disable CA1068, CA1822

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    internal class SRDispatchArchiveInfo
    {
        public uint   MajorVersion          { get; set; }
        public uint   MinorVersion          { get; set; }
        public uint   PatchVersion          { get; set; }
        public string ContentHash           { get; set; }
        public uint   FileSize              { get; set; }
        public uint   TimeStamp             { get; set; }
        public string FileName              { get; set; }
        public string BaseAssetsDownloadUrl { get; set; }
        public string FullAssetsDownloadUrl { get; set; }
    }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(SRDispatchArchiveInfo))]
    internal sealed partial class SRDispatchArchiveInfoContext : JsonSerializerContext;

    internal sealed partial class SRDispatcherInfo : IDisposable
    {
        private  string                PersistentDirectory { get; set; }
        private  string                DispatchURLFormat   { get; }
        private  string                GatewayURLFormat    { get; }
        private  string                DispatchURL         { get; }
        private  string                DispatchSeed        { get; }
        private  string                ProductID           { get; }
        private  string                ProductVer          { get; }
        private  CancellationToken     ThreadToken         { get; set; }
        internal string                RegionName          { get; set; }
        internal RegionInfo            RegionInfo          { get; set; }
        internal StarRailGateway       RegionGatewayLegacy { get; set; }
        internal StarRailGatewayStatic RegionGateway       { get; set; }
        internal bool                  IsUseLegacy         { get => false; }

        internal Dictionary<string, SRDispatchArchiveInfo>  ArchiveInfo { get; set; }

        internal SRDispatcherInfo(string dispatchURL, string dispatchSeed, string dispatchFormatTemplate, string gatewayFormatTemplate, string productID, string productVer)
        {
            DispatchURL            = dispatchURL;
            DispatchSeed           = dispatchSeed;
            ProductID              = productID;
            ProductVer             = productVer;
            DispatchURLFormat      = dispatchFormatTemplate;
            GatewayURLFormat       = gatewayFormatTemplate;
        }

        ~SRDispatcherInfo() => Dispose();

        public void Dispose()
        {
            ArchiveInfo?.Clear();
            RegionInfo = null;
            GC.SuppressFinalize(this);
        }

        internal async Task Initialize(CancellationToken threadToken, DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string persistentDirectory, string regionName)
        {
            PersistentDirectory = persistentDirectory;
            ThreadToken = threadToken;
            RegionName = regionName;

            await ParseDispatch(downloadClient, downloadProgressDelegate);
            await ParseGateway(downloadClient, downloadProgressDelegate);
            await ParseArchive(downloadClient, downloadProgressDelegate);
        }

        private async Task ParseDispatch(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate)
        {
            // Format dispatcher URL
            string dispatchURL = DispatchURL + string.Format(DispatchURLFormat, ProductID, ProductVer, SRMetadata.GetUnixTimestamp());

#if DEBUG
            Console.WriteLine($"Dispatch URL: {dispatchURL}");
#endif

            // Get the dispatch content
            using MemoryStream stream = new MemoryStream();
            await downloadClient.DownloadAsync(dispatchURL, stream, false, downloadProgressDelegate, cancelToken: ThreadToken);
            stream.Position = 0;
            string response = Encoding.UTF8.GetString(stream.ToArray());

#if DEBUG
            Console.WriteLine($"Response (in Base64): {response}");
        #endif

            byte[] content = Convert.FromBase64String(response);

            // Deserialize dispatcher and assign the region
            StarRailDispatch dispatch = StarRailDispatch.Parser.ParseFrom(content);
            RegionInfo = dispatch.RegionList.FirstOrDefault(x => x.Name == RegionName);

            if (RegionInfo == null) throw new KeyNotFoundException($"Region: {RegionName} is not found in the dispatcher! Ignore this error if the game is under maintenance.");
        }

        private async Task ParseGateway(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate)
        {
            // Format dispatcher URL
            string gatewayURL = RegionInfo.DispatchUrl + string.Format(GatewayURLFormat, ProductID, ProductVer, SRMetadata.GetUnixTimestamp(), DispatchSeed);

        #if DEBUG
            Console.WriteLine($"Gateway URL: {gatewayURL}");
        #endif

            // Get the dispatch content
            using MemoryStream stream = new MemoryStream();
            await downloadClient.DownloadAsync(gatewayURL, stream, false, downloadProgressDelegate, cancelToken: ThreadToken);
            stream.Position = 0;
            string response = Encoding.UTF8.GetString(stream.ToArray());

        #if DEBUG
            Console.WriteLine($"Response (in Base64): {response}");
        #endif

            byte[] content = Convert.FromBase64String(response);

                
                
            // Deserialize gateway
            if (IsUseLegacy)
                RegionGatewayLegacy = StarRailGateway.Parser.ParseFrom(content);
            else
                RegionGateway = StarRailGatewayStatic.Parser.ParseFrom(content);
        }

        private async Task ParseArchive(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate)
        {
            /* ===============================
             * Archive_V region
             * ===============================
             */
            ArchiveInfo = new Dictionary<string, SRDispatchArchiveInfo>();
            string archiveURL = (IsUseLegacy ? RegionGatewayLegacy.AssetBundleVersionUpdateUrl : RegionGateway.ValuePairs["AssetBundleVersionUpdateUrl"]) + "/client/Windows/Archive/M_ArchiveV.bytes";
            string localPath = Path.Combine(PersistentDirectory, @"Archive\Windows\M_ArchiveV_cache.bytes");

#if DEBUG
            Console.WriteLine($"Parsing Archive -----");
            Console.WriteLine($"    URL: {archiveURL}");
            Console.WriteLine($"    LocalPath: {localPath}");
#endif

            // Parse M_ArchiveV first
            await DownloadAndParseArchiveInfo(downloadClient, downloadProgressDelegate, "AssetBundleVersionUpdateUrl", archiveURL, localPath);

            /* ===============================
             * Design_Archive_V region
             * ===============================
             */
            archiveURL = (IsUseLegacy ? RegionGatewayLegacy.DesignDataBundleVersionUpdateUrl : RegionGateway.ValuePairs["DesignDataBundleVersionUpdateUrl"]) + "/client/Windows/M_Design_ArchiveV.bytes";

#if DEBUG
            Console.WriteLine($"Parsing Design Archive -----");
            Console.WriteLine($"    URL: {archiveURL}");
#endif
            // Get HttpClient and response
            HttpClient client = downloadClient.GetHttpClient();
            using HttpResponseMessage designArchiveHttpResponse = await client.GetAsync(archiveURL, ThreadToken);
            // Skip if the design archive is unreachable
            if (!designArchiveHttpResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("Cannot parse Design Archive resource as HTTP response returns a non-success code: {0} ({1})",
                    designArchiveHttpResponse.StatusCode,
                    designArchiveHttpResponse.StatusCode.ToString());
                return;
            }

            // Parse design archive and get Native Data resources
            await using Stream designArchiveStream = await designArchiveHttpResponse.Content.ReadAsStreamAsync(ThreadToken);
            await ParseArchiveInfoFromStream(designArchiveStream, "DesignDataBundleVersionUpdateUrl", ThreadToken);

            const string NativeDataRefDictKey  = "M_NativeDataV";
            const string NativeDataDataDictKey = "NativeDataV_";
            if (ArchiveInfo.TryGetValue(NativeDataRefDictKey, out SRDispatchArchiveInfo nativeDataArchiveInfo))
            {
                const string localNativeDataRefName = NativeDataRefDictKey + ".bytes";
                string localNativeDataRefUrl = ConverterTool.CombineURLFromString(nativeDataArchiveInfo.FullAssetsDownloadUrl, localNativeDataRefName);
                string localNativeDataRefPath = Path.Combine(PersistentDirectory, "NativeData\\Windows", localNativeDataRefName);
#if DEBUG
                Console.WriteLine($"    NativeDataRefUrl: {localNativeDataRefUrl}");
                Console.WriteLine($"    NativeDataRefPath: {localNativeDataRefPath}");
#endif
                await DownloadArchiveInfo(downloadClient, downloadProgressDelegate, localNativeDataRefUrl, localNativeDataRefPath);

                string localNativeDataResName = NativeDataDataDictKey + nativeDataArchiveInfo.ContentHash + ".bytes";
                string localNativeDataResUrl = ConverterTool.CombineURLFromString(nativeDataArchiveInfo.FullAssetsDownloadUrl, localNativeDataResName);
                string localNativeDataResPath = Path.Combine(PersistentDirectory, "NativeData\\Windows", localNativeDataResName);
#if DEBUG
                Console.WriteLine($"    NativeDataResUrl: {localNativeDataResUrl}");
                Console.WriteLine($"    NativeDataResPath: {localNativeDataResPath}");
#endif
                await DownloadArchiveInfo(downloadClient, downloadProgressDelegate, localNativeDataResUrl, localNativeDataResPath);
            }
        }

        private async Task DownloadArchiveInfo(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string archiveURL, string localPath)
        {
            EnsureDirectoryExistence(localPath);
            await using FileStream stream = new FileStream(localPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            await downloadClient.DownloadAsync(archiveURL, stream, false, downloadProgressDelegate, cancelToken: ThreadToken);
        }

        private async Task DownloadAndParseArchiveInfo(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string gatewayDictKey, string archiveURL, string localPath)
        {
            EnsureDirectoryExistence(localPath);
            await using FileStream stream = new FileStream(localPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            await downloadClient.DownloadAsync(archiveURL, stream, false, downloadProgressDelegate, cancelToken: ThreadToken);
            stream.Position = 0;
            await ParseArchiveInfoFromStream(stream, gatewayDictKey, ThreadToken);
        }

        private static void EnsureDirectoryExistence(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private async Task ParseArchiveInfoFromStream(Stream stream, string gatewayDictKey, CancellationToken token)
        {
            using StreamReader reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                string                line        = await reader.ReadLineAsync(token);
                SRDispatchArchiveInfo archiveInfo = JsonSerializer.Deserialize(line, SRDispatchArchiveInfoContext.Default.SRDispatchArchiveInfo);
                string baseUrl = string.IsNullOrEmpty(archiveInfo.BaseAssetsDownloadUrl) ?
                    RegionGateway.ValuePairs[gatewayDictKey] :
                    TrimLastURLRelativePath(IsUseLegacy ?
                                                RegionGatewayLegacy.AssetBundleVersionUpdateUrl :
                                                RegionGateway.ValuePairs[gatewayDictKey]
                                           );
                archiveInfo.FullAssetsDownloadUrl = 
                    ConverterTool.CombineURLFromString(
                                                       baseUrl,
                                                       archiveInfo.BaseAssetsDownloadUrl,
                                                       archiveInfo.FileName switch
                                                       {
                                                           "M_AudioV" => "/client/Windows/AudioBlock",
                                                           "M_VideoV" => "/client/Windows/Video",
                                                           "M_DesignV" or "M_Design_PatchV" => "/client/Windows",
                                                           "M_NativeDataV" => "/client/Windows/NativeData",
                                                           _ => "/client/Windows/Block"
                                                       }
                                                      );
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
