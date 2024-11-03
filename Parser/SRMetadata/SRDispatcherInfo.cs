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

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    internal struct SRDispatchArchiveInfo
    {
        public uint MajorVersion { get; set; }
        public uint MinorVersion { get; set; }
        public uint PatchVersion { get; set; }
        public string ContentHash { get; set; }
        public uint FileSize { get; set; }
        public uint TimeStamp { get; set; }
        public string FileName { get; set; }
        public string BaseAssetsDownloadUrl { get; set; }
        public string FullAssetsDownloadUrl { get; set; }
    }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(SRDispatchArchiveInfo))]
    internal sealed partial class SRDispatchArchiveInfoContext : JsonSerializerContext { }

    internal class SRDispatcherInfo : IDisposable
    {
        private string                      _persistentDirectory    { get; set; }
        private string                      _dispatchURLFormat      { get; set; }
        private string                      _gatewayURLFormat       { get; set; }
        private string                      _dispatchURL            { get; init; }
        private string                      _dispatchSeed           { get; init; }
        private string                      _productID              { get; init; }
        private string                      _productVer             { get; init; }
        private CancellationToken           _threadToken            { get; set; }

        internal string                 _regionName             { get; set; }
        internal RegionInfo             _regionInfo             { get; set; }
        internal StarRailGateway        _regionGatewayLegacy    { get; set; }
        internal StarRailGatewayStatic  _regionGateway          { get; set; }
        internal bool                   _isUseLegacy            { get => false; }

        internal Dictionary<string, SRDispatchArchiveInfo>  ArchiveInfo { get; set; }

        internal SRDispatcherInfo(string dispatchURL, string dispatchSeed, string dispatchFormatTemplate, string gatewayFormatTemplate, string productID, string productVer)
        {
            _dispatchURL            = dispatchURL;
            _dispatchSeed           = dispatchSeed;
            _productID              = productID;
            _productVer             = productVer;
            _dispatchURLFormat      = dispatchFormatTemplate;
            _gatewayURLFormat       = gatewayFormatTemplate;
        }

        ~SRDispatcherInfo() => Dispose();

        public void Dispose()
        {
            if (ArchiveInfo != null)
            {
                ArchiveInfo.Clear();
            }
            _regionInfo = null;
        }

        internal async Task Initialize(CancellationToken threadToken, DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string persistentDirectory, string regionName)
        {
            _persistentDirectory = persistentDirectory;
            _threadToken = threadToken;
            _regionName = regionName;

            await ParseDispatch(downloadClient, downloadProgressDelegate);
            await ParseGateway(downloadClient, downloadProgressDelegate);
            await ParseArchive(downloadClient, downloadProgressDelegate);
        }

        private async Task ParseDispatch(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate)
        {
            // Format dispatcher URL
            string dispatchURL = _dispatchURL + string.Format(_dispatchURLFormat, _productID, _productVer, SRMetadata.GetUnixTimestamp());

#if DEBUG
            Console.WriteLine($"Dispatch URL: {dispatchURL}");
#endif

            // Get the dispatch content
            using (MemoryStream stream = new MemoryStream())
            {
                await downloadClient.DownloadAsync(dispatchURL, stream, false, downloadProgressDelegate, cancelToken: _threadToken);
                stream.Position = 0;
                string response = Encoding.UTF8.GetString(stream.ToArray());

#if DEBUG
                Console.WriteLine($"Response (in Base64): {response}");
#endif

                byte[] content = Convert.FromBase64String(response);

                // Deserialize dispatcher and assign the region
                StarRailDispatch dispatch = StarRailDispatch.Parser.ParseFrom(content);
                _regionInfo = dispatch.RegionList.Where(x => x.Name == _regionName).FirstOrDefault();

                if (_regionInfo == null) throw new KeyNotFoundException($"Region: {_regionName} is not found in the dispatcher! Ignore this error if the game is under maintenance.");
            }
        }

        private async Task ParseGateway(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate)
        {
            // Format dispatcher URL
            string gatewayURL = _regionInfo.DispatchUrl + string.Format(_gatewayURLFormat, _productID, _productVer, SRMetadata.GetUnixTimestamp(), _dispatchSeed);

#if DEBUG
            Console.WriteLine($"Gateway URL: {gatewayURL}");
#endif

            // Get the dispatch content
            using (MemoryStream stream = new MemoryStream())
            {
                await downloadClient.DownloadAsync(gatewayURL, stream, false, downloadProgressDelegate, cancelToken: _threadToken);
                stream.Position = 0;
                string response = Encoding.UTF8.GetString(stream.ToArray());

#if DEBUG
                Console.WriteLine($"Response (in Base64): {response}");
#endif

                byte[] content = Convert.FromBase64String(response);

                
                
                // Deserialize gateway
                if (_isUseLegacy)
                    _regionGatewayLegacy = StarRailGateway.Parser.ParseFrom(content);
                else
                    _regionGateway = StarRailGatewayStatic.Parser.ParseFrom(content);
            }
        }

        private async Task ParseArchive(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate)
        {
            /* ===============================
             * Archive_V region
             * ===============================
             */
            ArchiveInfo = new Dictionary<string, SRDispatchArchiveInfo>();
            string archiveURL = (_isUseLegacy ? _regionGatewayLegacy.AssetBundleVersionUpdateUrl : _regionGateway.ValuePairs["AssetBundleVersionUpdateUrl"]) + "/client/Windows/Archive/M_ArchiveV.bytes";
            string localPath = Path.Combine(_persistentDirectory, "Archive\\Windows\\M_ArchiveV_cache.bytes");

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
            archiveURL = (_isUseLegacy ? _regionGatewayLegacy.DesignDataBundleVersionUpdateUrl : _regionGateway.ValuePairs["DesignDataBundleVersionUpdateUrl"]) + "/client/Windows/M_Design_ArchiveV.bytes";

#if DEBUG
            Console.WriteLine($"Parsing Design Archive -----");
            Console.WriteLine($"    URL: {archiveURL}");
#endif
            // Get HttpClient and response
            HttpClient client = downloadClient.GetHttpClient();
            using HttpResponseMessage designArchiveHttpResponse = await client.GetAsync(archiveURL, _threadToken);
            // Skip if the design archive is unreachable
            if (!designArchiveHttpResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("Cannot parse Design Archive resource as HTTP response returns a non-success code: {0} ({1})",
                    designArchiveHttpResponse.StatusCode,
                    designArchiveHttpResponse.StatusCode.ToString());
                return;
            }

            // Parse design archive and get Native Data resources
            await using Stream designArchiveStream = await designArchiveHttpResponse.Content.ReadAsStreamAsync(_threadToken);
            await ParseArchiveInfoFromStream(designArchiveStream, "DesignDataBundleVersionUpdateUrl", _threadToken);

            const string NativeDataRefDictKey = "M_NativeDataV";
            const string NativeDataDataDictKey = "NativeDataV_";
            if (ArchiveInfo.TryGetValue(NativeDataRefDictKey, out SRDispatchArchiveInfo nativeDataArchiveInfo))
            {
                string localNativeDataRefName = NativeDataRefDictKey + ".bytes";
                string localNativeDataRefUrl = ConverterTool.CombineURLFromString(nativeDataArchiveInfo.FullAssetsDownloadUrl, localNativeDataRefName);
                string localNativeDataRefPath = Path.Combine(_persistentDirectory, "NativeData\\Windows", localNativeDataRefName);
#if DEBUG
                Console.WriteLine($"    NativeDataRefUrl: {localNativeDataRefUrl}");
                Console.WriteLine($"    NativeDataRefPath: {localNativeDataRefPath}");
#endif
                await DownloadArchiveInfo(downloadClient, downloadProgressDelegate, localNativeDataRefUrl, localNativeDataRefName);

                string localNativeDataResName = NativeDataDataDictKey + nativeDataArchiveInfo.ContentHash + ".bytes";
                string localNativeDataResUrl = ConverterTool.CombineURLFromString(nativeDataArchiveInfo.FullAssetsDownloadUrl, localNativeDataResName);
                string localNativeDataResPath = Path.Combine(_persistentDirectory, "NativeData\\Windows", localNativeDataResName);
#if DEBUG
                Console.WriteLine($"    NativeDataResUrl: {localNativeDataResUrl}");
                Console.WriteLine($"    NativeDataResPath: {localNativeDataResPath}");
#endif
                await DownloadArchiveInfo(downloadClient, downloadProgressDelegate, localNativeDataResUrl, localNativeDataResPath);
            }
        }

        private async Task DownloadArchiveInfo(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string archiveURL, string localPath)
        {
            EnsureDirectoryExistance(localPath);
            using (FileStream stream = new FileStream(localPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                await downloadClient.DownloadAsync(archiveURL, stream, false, downloadProgressDelegate, cancelToken: _threadToken);
            }
        }

        private async Task DownloadAndParseArchiveInfo(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string gatewayDictKey, string archiveURL, string localPath)
        {
            EnsureDirectoryExistance(localPath);
            using (FileStream stream = new FileStream(localPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                await downloadClient.DownloadAsync(archiveURL, stream, false, downloadProgressDelegate, cancelToken: _threadToken);
                stream.Position = 0;
                await ParseArchiveInfoFromStream(stream, gatewayDictKey, _threadToken);
            }
        }

        private void EnsureDirectoryExistance(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private async Task ParseArchiveInfoFromStream(Stream stream, string gatewayDictKey, CancellationToken token)
        {
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    string line = await reader.ReadLineAsync(token);
                    SRDispatchArchiveInfo archiveInfo = JsonSerializer.Deserialize(line, SRDispatchArchiveInfoContext.Default.SRDispatchArchiveInfo);
                    string baseUrl = string.IsNullOrEmpty(archiveInfo.BaseAssetsDownloadUrl) ?
                        _regionGateway.ValuePairs[gatewayDictKey] :
                        TrimLastURLRelativePath(_isUseLegacy ?
                                _regionGatewayLegacy.AssetBundleVersionUpdateUrl :
                                _regionGateway.ValuePairs[gatewayDictKey]
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
        }

        private string TrimLastURLRelativePath(string url)
        {
            string[] urlPart = url.Split('/');

            string ret = string.Join('/', urlPart[..(urlPart.Length - 1)]);
            return ret;
        }
    }
}
