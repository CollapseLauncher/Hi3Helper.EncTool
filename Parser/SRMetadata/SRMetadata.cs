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
        private bool _isInitialized { get; set; }

        public SRMetadataBase MetadataIFix { get; set; }
        public SRMetadataBase MetadataDesign { get; set; }
        public SRMetadataBase MetadataAsb { get; set; }
        public SRMetadataBase MetadataBlock { get; set; }
        public SRMetadataBase MetadataLua { get; set; }
        public SRMetadataBase MetadataAudio { get; set; }
        public SRMetadataBase MetadataVideo { get; set; }

        public SRMetadata(string dispatchURL, string dispatchSeed, string dispatchFormatTemplate, string gatewayFormatTemplate, string productID, string productVer)
        {
            _httpClient = new Http.Http(true, 5, 1000, _userAgent);
            _dispatcherInfo = new SRDispatcherInfo(_httpClient, dispatchURL, dispatchSeed, dispatchFormatTemplate, gatewayFormatTemplate, productID, productVer);
            _isInitialized = false;
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

            _isInitialized = false;
        }

        public async Task Initialize(CancellationToken threadToken, byte regionID, bool forceInitialize = false)
        {
            if (!_isInitialized || forceInitialize)
            {
                try
                {
                    _httpClient.DownloadProgress += HttpProgressAdapter;
                    await _dispatcherInfo.Initialize(threadToken, regionID);

                    _isInitialized = true;
                }
                catch { throw; }
                finally
                {
                    _httpClient.DownloadProgress -= HttpProgressAdapter;
                }
            }
        }

        public async Task ReadIFixMetadataInformation(CancellationToken threadToken)
        {
            try
            {
                _httpClient.DownloadProgress += HttpProgressAdapter;

                MetadataIFix = SRIFixMetadata.CreateInstance(_dispatcherInfo._regionGateway.IFixPatchVersionUpdateUrl, _httpClient);
                await MetadataIFix.GetRemoteMetadata(threadToken);
                MetadataIFix.Deserialize();
            }
            catch { throw; }
            finally
            {
                _httpClient.DownloadProgress -= HttpProgressAdapter;
            }
        }

        public async Task ReadDesignMetadataInformation(CancellationToken threadToken)
        {
            try
            {
                _httpClient.DownloadProgress += HttpProgressAdapter;

                MetadataDesign = SRDesignMetadata.CreateInstance(_dispatcherInfo._regionGateway.DesignDataBundleVersionUpdateUrl, _httpClient);
                await MetadataDesign.GetRemoteMetadata(threadToken);
                MetadataDesign.Deserialize();
            }
            catch { throw; }
            finally
            {
                _httpClient.DownloadProgress -= HttpProgressAdapter;
            }
        }

        public async Task ReadLuaMetadataInformation(CancellationToken threadToken)
        {
            try
            {
                _httpClient.DownloadProgress += HttpProgressAdapter;

                MetadataLua = SRLuaMetadata.CreateInstance(_dispatcherInfo._regionGateway.LuaBundleVersionUpdateUrl, _httpClient);
                await MetadataLua.GetRemoteMetadata(threadToken);
                MetadataLua.Deserialize();
            }
            catch { throw; }
            finally
            {
                _httpClient.DownloadProgress -= HttpProgressAdapter;
            }
        }

        public async Task ReadAsbMetadataInformation(CancellationToken threadToken)
        {
            try
            {
                _httpClient.DownloadProgress += HttpProgressAdapter;

                MetadataAsb = SRAsbMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._regionGateway.AssetBundleVersionUpdateUrl, _httpClient);
                await MetadataAsb.GetRemoteMetadata(threadToken);
                MetadataAsb.Deserialize();
            }
            catch { throw; }
            finally
            {
                _httpClient.DownloadProgress -= HttpProgressAdapter;
            }
        }

        public async Task ReadBlockMetadataInformation(CancellationToken threadToken)
        {
            try
            {
                _httpClient.DownloadProgress += HttpProgressAdapter;

                MetadataBlock = SRBlockMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._regionGateway.AssetBundleVersionUpdateUrl, _httpClient);
                await MetadataBlock.GetRemoteMetadata(threadToken);
                MetadataBlock.Deserialize();
            }
            catch { throw; }
            finally
            {
                _httpClient.DownloadProgress -= HttpProgressAdapter;
            }
        }

        public async Task ReadAudioMetadataInformation(CancellationToken threadToken)
        {
            try
            {
                _httpClient.DownloadProgress += HttpProgressAdapter;

                MetadataAudio = SRAudioMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._regionGateway.AssetBundleVersionUpdateUrl, _httpClient);
                await MetadataAudio.GetRemoteMetadata(threadToken);
                MetadataAudio.Deserialize();
            }
            catch { throw; }
            finally
            {
                _httpClient.DownloadProgress -= HttpProgressAdapter;
            }
        }

        public async Task ReadVideoMetadataInformation(CancellationToken threadToken)
        {
            try
            {
                _httpClient.DownloadProgress += HttpProgressAdapter;

                MetadataVideo = SRVideoMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._regionGateway.AssetBundleVersionUpdateUrl, _httpClient);
                await MetadataVideo.GetRemoteMetadata(threadToken);
                MetadataVideo.Deserialize();
            }
            catch { throw; }
            finally
            {
                _httpClient.DownloadProgress -= HttpProgressAdapter;
            }
        }

        private void HttpProgressAdapter(object sender, DownloadEvent e) => HttpEvent?.Invoke(this, e);

        internal static int GetUnixTimestamp(bool isUTC = true) => (int)Math.Truncate(isUTC ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
    }
}
