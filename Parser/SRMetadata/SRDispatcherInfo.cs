using Hi3Helper.EncTool.Proto.StarRail;
using System;
using System.Collections.Generic;
using System.IO;
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

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(SRDispatchArchiveInfo))]
    internal partial class SRDispatchArchiveInfoContext : JsonSerializerContext { }

    internal class SRDispatcherInfo
    {
        private const string _dispatchURLFormat = "{0}?version={1}{2}&t={3}&language_type=3&platform_type=3&channel_id=1&sub_channel_id=1&is_new_format=1";
        private const string _gatewayURLFormat = "{0}?version={1}{2}&t={3}&uid=0&language_type=3&platform_type=3&dispatch_seed={4}&channel_id=1&sub_channel_id=1&is_need_url=1";

        private string _dispatchURL { get; init; }
        private string _dispatchSeed { get; init; }
        private string _productID { get; init; }
        private string _productVer { get; init; }
        private Http.Http _httpClient { get; set; }
        private CancellationToken _threadToken { get; set; }

        internal byte _regionID { get; set; }
        internal RegionInfo _regionInfo { get; set; }
        internal StarRailGateway _regionGateway { get; set; }

        internal Dictionary<string, SRDispatchArchiveInfo> ArchiveInfo { get; set; }

        internal SRDispatcherInfo(Http.Http httpClient, string dispatchURL, string dispatchSeed, string productID, string productVer)
        {
            _httpClient = httpClient;
            _dispatchURL = dispatchURL;
            _dispatchSeed = dispatchSeed;
            _productID = productID;
            _productVer = productVer;
            _httpClient = new Http.Http(true, 5, 1000, SRMetadata._userAgent);
        }

        internal async Task Initialize(CancellationToken threadToken, byte regionID)
        {
            _threadToken = threadToken;
            _regionID = regionID;

            await ParseDispatch();
            await ParseGateway();
            await ParseArchive();
        }

        private async Task ParseDispatch()
        {
            // Format dispatcher URL
            string dispatchURL = string.Format(_dispatchURLFormat, _dispatchURL, _productID, _productVer, SRMetadata.GetUnixTimestamp());

            // Get the dispatch content
            using (MemoryStream stream = new MemoryStream())
            {
                await _httpClient.Download(dispatchURL, stream, null, null, _threadToken);
                stream.Position = 0;
                byte[] content = Convert.FromBase64String(Encoding.UTF8.GetString(stream.ToArray()));

                // Deserialize dispatcher and assign the region
                StarRailDispatch dispatch = StarRailDispatch.Parser.ParseFrom(content);
                _regionInfo = dispatch.RegionList[_regionID];
            }
        }

        private async Task ParseGateway()
        {
            // Format dispatcher URL
            string gatewayURL = string.Format(_gatewayURLFormat, _regionInfo.DispatchUrl, _productID, _productVer, SRMetadata.GetUnixTimestamp(), _dispatchSeed);

            // Get the dispatch content
            using (MemoryStream stream = new MemoryStream())
            {
                await _httpClient.Download(gatewayURL, stream, null, null, _threadToken);
                stream.Position = 0;
                byte[] content = Convert.FromBase64String(Encoding.UTF8.GetString(stream.ToArray()));

                // Deserialize gateway
                _regionGateway = StarRailGateway.Parser.ParseFrom(content);
            }
        }

        private async Task ParseArchive()
        {
            ArchiveInfo = new Dictionary<string, SRDispatchArchiveInfo>();
            string archiveURL = _regionGateway.AssetBundleVersionUpdateUrl + "/client/Windows/Archive/M_ArchiveV.bytes";

            using (MemoryStream stream = new MemoryStream())
            {
                await _httpClient.Download(archiveURL, stream, null, null, _threadToken);
                stream.Position = 0;

                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        SRDispatchArchiveInfo archiveInfo = (SRDispatchArchiveInfo)JsonSerializer.Deserialize(line, typeof(SRDispatchArchiveInfo), SRDispatchArchiveInfoContext.Default);
                        archiveInfo.FullAssetsDownloadUrl = TrimLastURLRelativePath(_regionGateway.AssetBundleVersionUpdateUrl)
                            + '/' + archiveInfo.BaseAssetsDownloadUrl + (archiveInfo.FileName switch
                            {
                                "M_AudioV" => "/client/Windows/AudioBlock",
                                "M_VideoV" => "/client/Windows/VideoBlock",
                                _ => "/client/Windows/Block"
                            });
                        ArchiveInfo.Add(archiveInfo.FileName, archiveInfo);
                    }
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
