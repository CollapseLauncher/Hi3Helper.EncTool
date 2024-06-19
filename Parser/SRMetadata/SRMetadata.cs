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

        private string           _persistentPath { get; set; }
        private SRDispatcherInfo _dispatcherInfo { get; init; }
        private Http.Http        _httpClient     { get; set; }
        private bool             _isInitialized  { get; set; }

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
            _dispatcherInfo?.Dispose();
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

        public async Task<bool> Initialize(CancellationToken threadToken, string regionName, string persistentDirectory, bool forceInitialize = false)
        {
            if (!_isInitialized || forceInitialize)
            {
                try
                {
                    _httpClient.DownloadProgress += HttpProgressAdapter;
                    _persistentPath = persistentDirectory;
                    await _dispatcherInfo.Initialize(threadToken, persistentDirectory, regionName);

                    _isInitialized = true;
                }
                catch
                {
#if DEBUG
                    throw;
#else
                    return false;
#endif
                }
                finally
                {
                    _httpClient.DownloadProgress -= HttpProgressAdapter;
                }
            }
            return true;
        }

        public async Task ReadIFixMetadataInformation(CancellationToken threadToken)
        {
            try
            {
                _httpClient.DownloadProgress += HttpProgressAdapter;
                
                MetadataIFix = SRIFixMetadata.CreateInstance(_dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.IFixPatchVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["IFixPatchVersionUpdateUrl"], _httpClient);
                await MetadataIFix.GetRemoteMetadata(_persistentPath, threadToken, "IFix\\Windows");
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

                MetadataDesign = SRDesignMetadata.CreateInstance(_dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.DesignDataBundleVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["DesignDataBundleVersionUpdateUrl"], _httpClient);
                await MetadataDesign.GetRemoteMetadata(_persistentPath, threadToken, "DesignData\\Windows");
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

                MetadataLua = SRLuaMetadata.CreateInstance(_dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.LuaBundleVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["LuaBundleVersionUpdateUrl"], _httpClient);
                await MetadataLua.GetRemoteMetadata(_persistentPath, threadToken, "Lua\\Windows");
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

                MetadataAsb = SRAsbMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.AssetBundleVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["AssetBundleVersionUpdateUrl"], _httpClient);
                await MetadataAsb.GetRemoteMetadata(_persistentPath, threadToken, "Asb\\Windows");
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

                MetadataBlock = SRBlockMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.AssetBundleVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["AssetBundleVersionUpdateUrl"], _httpClient);
                await MetadataBlock.GetRemoteMetadata(_persistentPath, threadToken, "Asb\\Windows");
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

                MetadataAudio = SRAudioMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.AssetBundleVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["AssetBundleVersionUpdateUrl"], _httpClient);
                await MetadataAudio.GetRemoteMetadata(_persistentPath, threadToken, "Audio\\AudioPackage\\Windows");
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

                MetadataVideo = SRVideoMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.AssetBundleVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["AssetBundleVersionUpdateUrl"], _httpClient);
                await MetadataVideo.GetRemoteMetadata(_persistentPath, threadToken, "Video\\Windows");
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
