using System.Collections.Generic;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal partial class SRVideoMetadata : SRAudioMetadata
    {
        protected SRVideoMetadata(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) : base(dictArchiveInfo, baseURL)
        {
            ParentRemotePath = "/client/Windows/Video";
            MetadataRemoteName = "M_VideoV";
            MetadataType = SRAMBMMetadataType.JSON;
            AssetType = SRAssetType.Video;
        }

        internal static new SRMetadataBase CreateInstance(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) => new SRVideoMetadata(dictArchiveInfo, baseURL);
    }
}
