using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Hi3Helper.Http;
using System;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable InconsistentNaming
// ReSharper disable PartialTypeWithSinglePart

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public sealed partial class SRMetadata(
        string dispatchURL,
        string dispatchSeed,
        string dispatchFormatTemplate,
        string gatewayFormatTemplate,
        string productID,
        string productVer)
        : IDisposable
    {
        private string           PersistentPath { get; set; }
        private SRDispatcherInfo DispatcherInfo { get; } =
            new SRDispatcherInfo(dispatchURL, dispatchSeed, dispatchFormatTemplate, gatewayFormatTemplate, productID,
                                 productVer);
        private bool             IsInitialized  { get; set; }

        public SRMetadataBase   MetadataIFix    { get; set; }
        public SRMetadataBase   MetadataDesign  { get; set; }
        public SRMetadataBase   MetadataAsb     { get; set; }
        public SRMetadataBase   MetadataBlock   { get; set; }
        public SRMetadataBase   MetadataLua     { get; set; }
        public SRMetadataBase   MetadataAudio   { get; set; }
        public SRMetadataBase   MetadataVideo   { get; set; }

        public void Dispose()
        {
            DispatcherInfo?.Dispose();
            MetadataIFix?.Dispose();
            MetadataDesign?.Dispose();
            MetadataAsb?.Dispose();
            MetadataBlock?.Dispose();
            MetadataLua?.Dispose();
            MetadataAudio?.Dispose();
            MetadataVideo?.Dispose();

            MetadataIFix   = null;
            MetadataDesign = null;
            MetadataAsb    = null;
            MetadataBlock  = null;
            MetadataLua    = null;
            MetadataAudio  = null;
            MetadataVideo  = null;

            IsInitialized = false;
        }

        public async Task<bool> Initialize(CancellationToken threadToken, DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string regionName, string persistentDirectory, bool forceInitialize = false)
        {
            if (IsInitialized && !forceInitialize)
            {
                return true;
            }

            PersistentPath = persistentDirectory;
            await DispatcherInfo.Initialize(threadToken, downloadClient, downloadProgressDelegate, persistentDirectory, regionName);

            IsInitialized = true;
            return true;
        }

        public async Task ReadIFixMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            MetadataIFix = SRIFixMetadata.CreateInstance(DispatcherInfo.IsUseLegacy ? DispatcherInfo.RegionGatewayLegacy.IFixPatchVersionUpdateUrl : DispatcherInfo.RegionGateway.ValuePairs["IFixPatchVersionUpdateUrl"]);
            await MetadataIFix.GetRemoteMetadata(downloadClient, downloadProgressDelegate, PersistentPath, threadToken, "IFix\\Windows");
            MetadataIFix.Deserialize();
        }

        public async Task ReadDesignMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            MetadataDesign = SRDesignMetadata.CreateInstance(DispatcherInfo.IsUseLegacy ? DispatcherInfo.RegionGatewayLegacy.DesignDataBundleVersionUpdateUrl : DispatcherInfo.RegionGateway.ValuePairs["DesignDataBundleVersionUpdateUrl"]);
            await MetadataDesign.GetRemoteMetadata(downloadClient, downloadProgressDelegate, PersistentPath, threadToken, "DesignData\\Windows");
            MetadataDesign.Deserialize();
        }

        public async Task ReadLuaMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            MetadataLua = SRLuaMetadata.CreateInstance(DispatcherInfo.IsUseLegacy ? DispatcherInfo.RegionGatewayLegacy.LuaBundleVersionUpdateUrl : DispatcherInfo.RegionGateway.ValuePairs["LuaBundleVersionUpdateUrl"]);
            await MetadataLua.GetRemoteMetadata(downloadClient, downloadProgressDelegate, PersistentPath, threadToken, "Lua\\Windows");
            MetadataLua.Deserialize();
        }

        public async Task ReadAsbMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            MetadataAsb = SRAsbMetadata.CreateInstance(DispatcherInfo.ArchiveInfo, DispatcherInfo.IsUseLegacy ? DispatcherInfo.RegionGatewayLegacy.AssetBundleVersionUpdateUrl : DispatcherInfo.RegionGateway.ValuePairs["AssetBundleVersionUpdateUrl"]);
            await MetadataAsb.GetRemoteMetadata(downloadClient, downloadProgressDelegate, PersistentPath, threadToken, "Asb\\Windows");
            MetadataAsb.Deserialize();
        }

        public async Task ReadBlockMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            MetadataBlock = SRBlockMetadata.CreateInstance(DispatcherInfo.ArchiveInfo, DispatcherInfo.IsUseLegacy ? DispatcherInfo.RegionGatewayLegacy.AssetBundleVersionUpdateUrl : DispatcherInfo.RegionGateway.ValuePairs["AssetBundleVersionUpdateUrl"]);
            await MetadataBlock.GetRemoteMetadata(downloadClient, downloadProgressDelegate, PersistentPath, threadToken, "Asb\\Windows");
            MetadataBlock.Deserialize();
        }

        public async Task ReadAudioMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            MetadataAudio = SRAudioMetadata.CreateInstance(DispatcherInfo.ArchiveInfo, DispatcherInfo.IsUseLegacy ? DispatcherInfo.RegionGatewayLegacy.AssetBundleVersionUpdateUrl : DispatcherInfo.RegionGateway.ValuePairs["AssetBundleVersionUpdateUrl"]);
            await MetadataAudio.GetRemoteMetadata(downloadClient, downloadProgressDelegate, PersistentPath, threadToken, "Audio\\AudioPackage\\Windows");
            MetadataAudio.Deserialize();
        }

        public async Task ReadVideoMetadataInformation(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, CancellationToken threadToken)
        {
            MetadataVideo = SRVideoMetadata.CreateInstance(DispatcherInfo.ArchiveInfo, DispatcherInfo.IsUseLegacy ? DispatcherInfo.RegionGatewayLegacy.AssetBundleVersionUpdateUrl : DispatcherInfo.RegionGateway.ValuePairs["AssetBundleVersionUpdateUrl"]);
            await MetadataVideo.GetRemoteMetadata(downloadClient, downloadProgressDelegate, PersistentPath, threadToken, "Video\\Windows");
            MetadataVideo.Deserialize();
        }

        internal static int GetUnixTimestamp(bool isUTC = true) => (int)Math.Truncate(isUTC ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
    }
}
