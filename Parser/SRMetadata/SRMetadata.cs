using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Hi3Helper.Http;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public class SRMetadata : IDisposable
    {
        internal const string _userAgent = "UnityPlayer/2019.4.34f1 (UnityWebRequest/1.0, libcurl/7.75.0-DEV)";

        public event EventHandler<DownloadEvent> HttpEvent;

        private SRDispatcherInfo _dispatcherInfo { get; init; }
        private Http.Http _httpClient { get; set; }

        public SRMetadataBase MetadataIFix { get; set; }
        public SRMetadataBase MetadataDesign { get; set; }
        public SRMetadataBase MetadataAsb { get; set; }
        public SRMetadataBase MetadataBlock { get; set; }
        public SRMetadataBase MetadataLua { get; set; }
        public SRMetadataBase MetadataAudio { get; set; }
        public SRMetadataBase MetadataVideo { get; set; }

        public SRMetadata(string dispatchURL, string dispatchSeed, string productID, string productVer)
        {
            _httpClient = new Http.Http(true, 5, 1000, _userAgent);
            _dispatcherInfo = new SRDispatcherInfo(_httpClient, dispatchURL, dispatchSeed, productID, productVer);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            MetadataIFix?.Dispose();
            MetadataDesign?.Dispose();
            MetadataAsb?.Dispose();
            MetadataBlock?.Dispose();
            MetadataLua?.Dispose();
            MetadataAudio?.Dispose();
            MetadataVideo?.Dispose();

            MetadataIFix = null;
            MetadataDesign = null;
            MetadataAsb = null;
            MetadataBlock = null;
            MetadataLua = null;
            MetadataAudio = null;
            MetadataVideo = null;
        }

        public async Task Initialize(CancellationToken threadToken, byte regionID)
        {
            _httpClient.DownloadProgress += HttpProgressAdapter;
            await _dispatcherInfo.Initialize(threadToken, regionID);
            _httpClient.DownloadProgress -= HttpProgressAdapter;
        }

        public async Task ReadIFixMetadataInformation(CancellationToken threadToken)
        {
            _httpClient.DownloadProgress += HttpProgressAdapter;

            MetadataIFix = SRIFixMetadata.CreateInstance(_dispatcherInfo._regionGateway.IFixPatchVersionUpdateUrl, _httpClient);
            await MetadataIFix.GetRemoteMetadata(threadToken);
            MetadataIFix.Deserialize();

            _httpClient.DownloadProgress -= HttpProgressAdapter;
        }

        public async Task ReadLuaMetadataInformation(CancellationToken threadToken)
        {
            _httpClient.DownloadProgress += HttpProgressAdapter;

            MetadataLua = SRLuaMetadata.CreateInstance(_dispatcherInfo._regionGateway.LuaBundleVersionUpdateUrl, _httpClient);
            await MetadataLua.GetRemoteMetadata(threadToken);
            MetadataLua.Deserialize();

            _httpClient.DownloadProgress -= HttpProgressAdapter;
        }

        public async Task ReadAsbMetadataInformation(CancellationToken threadToken)
        {
            _httpClient.DownloadProgress += HttpProgressAdapter;

            MetadataAsb = SRAsbMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._regionGateway.AssetBundleVersionUpdateUrl, _httpClient);
            await MetadataAsb.GetRemoteMetadata(threadToken);
            MetadataAsb.Deserialize();

            _httpClient.DownloadProgress -= HttpProgressAdapter;
        }

        public async Task ReadBlockMetadataInformation(CancellationToken threadToken)
        {
            _httpClient.DownloadProgress += HttpProgressAdapter;

            MetadataBlock = SRBlockMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._regionGateway.AssetBundleVersionUpdateUrl, _httpClient);
            await MetadataBlock.GetRemoteMetadata(threadToken);
            MetadataBlock.Deserialize();

            _httpClient.DownloadProgress -= HttpProgressAdapter;
        }

        private void HttpProgressAdapter(object sender, DownloadEvent e) => HttpEvent?.Invoke(this, e);

        internal static int GetUnixTimestamp(bool isUTC = true) => (int)Math.Truncate(isUTC ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
    }
}
