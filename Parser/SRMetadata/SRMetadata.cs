using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Hi3Helper.Http;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public partial class SRMetadata : IDisposable
    {
        private string                      _persistentPath         { get; set; }
        private SRDispatcherInfo            _dispatcherInfo         { get; init; }
        private bool                        _isInitialized          { get; set; }

        public SRMetadataBase   MetadataIFix    { get; set; }
        public SRMetadataBase   MetadataDesign  { get; set; }
        public SRMetadataBase   MetadataAsb     { get; set; }
        public SRMetadataBase   MetadataBlock   { get; set; }
        public SRMetadataBase   MetadataLua     { get; set; }
        public SRMetadataBase   MetadataAudio   { get; set; }
        public SRMetadataBase   MetadataVideo   { get; set; }

        public SRMetadata(string dispatchURL, string dispatchSeed, string dispatchFormatTemplate, string gatewayFormatTemplate, string productID, string productVer)
        {
            _dispatcherInfo = new SRDispatcherInfo(dispatchURL, dispatchSeed, dispatchFormatTemplate, gatewayFormatTemplate, productID, productVer);
            _isInitialized = false;
        }

        public void Dispose()
        {
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

        public async Task<bool> Initialize(CancellationToken threadToken, DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string regionName, string persistentDirectory, bool forceInitialize = false)
        {
            if (!_isInitialized || forceInitialize)
            {
                try
                {
                    _persistentPath = persistentDirectory;
                    await _dispatcherInfo.Initialize(threadToken, downloadClient, downloadProgressDelegate, persistentDirectory, regionName);

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
            }
            return true;
        }

        public async Task ReadIFixMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            try
            {
                MetadataIFix = SRIFixMetadata.CreateInstance(_dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.IFixPatchVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["IFixPatchVersionUpdateUrl"]);
                await MetadataIFix.GetRemoteMetadata(downloadClient, downloadProgressDelegate, _persistentPath, threadToken, "IFix\\Windows");
                MetadataIFix.Deserialize();
            }
            catch { throw; }
        }

        public async Task ReadDesignMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            try
            {
                MetadataDesign = SRDesignMetadata.CreateInstance(_dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.DesignDataBundleVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["DesignDataBundleVersionUpdateUrl"]);
                await MetadataDesign.GetRemoteMetadata(downloadClient, downloadProgressDelegate, _persistentPath, threadToken, "DesignData\\Windows");
                MetadataDesign.Deserialize();
            }
            catch { throw; }
        }

        public async Task ReadLuaMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            try
            {
                MetadataLua = SRLuaMetadata.CreateInstance(_dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.LuaBundleVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["LuaBundleVersionUpdateUrl"]);
                await MetadataLua.GetRemoteMetadata(downloadClient, downloadProgressDelegate, _persistentPath, threadToken, "Lua\\Windows");
                MetadataLua.Deserialize();
            }
            catch { throw; }
        }

        public async Task ReadAsbMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            try
            {
                MetadataAsb = SRAsbMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.AssetBundleVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["AssetBundleVersionUpdateUrl"]);
                await MetadataAsb.GetRemoteMetadata(downloadClient, downloadProgressDelegate, _persistentPath, threadToken, "Asb\\Windows");
                MetadataAsb.Deserialize();
            }
            catch { throw; }
        }

        public async Task ReadBlockMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            try
            {
                MetadataBlock = SRBlockMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.AssetBundleVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["AssetBundleVersionUpdateUrl"]);
                await MetadataBlock.GetRemoteMetadata(downloadClient, downloadProgressDelegate, _persistentPath, threadToken, "Asb\\Windows");
                MetadataBlock.Deserialize();
            }
            catch { throw; }
        }

        public async Task ReadAudioMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            try
            {
                MetadataAudio = SRAudioMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.AssetBundleVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["AssetBundleVersionUpdateUrl"]);
                await MetadataAudio.GetRemoteMetadata(downloadClient, downloadProgressDelegate, _persistentPath, threadToken, "Audio\\AudioPackage\\Windows");
                MetadataAudio.Deserialize();
            }
            catch { throw; }
        }

        public async Task ReadVideoMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            try
            {
                MetadataVideo = SRVideoMetadata.CreateInstance(_dispatcherInfo.ArchiveInfo, _dispatcherInfo._isUseLegacy ? _dispatcherInfo._regionGatewayLegacy.AssetBundleVersionUpdateUrl : _dispatcherInfo._regionGateway.ValuePairs["AssetBundleVersionUpdateUrl"]);
                await MetadataVideo.GetRemoteMetadata(downloadClient, downloadProgressDelegate, _persistentPath, threadToken, "Video\\Windows");
                MetadataVideo.Deserialize();
            }
            catch { throw; }
        }

        internal static int GetUnixTimestamp(bool isUTC = true) => (int)Math.Truncate(isUTC ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
    }
}
