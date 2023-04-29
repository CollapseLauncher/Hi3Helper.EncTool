using System.Collections.Generic;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal class SRVideoMetadata : SRAudioMetadata
    {
        protected SRVideoMetadata(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL, Http.Http httpClient) : base(dictArchiveInfo, baseURL, httpClient)
        {
            ParentRemotePath = "/client/Windows/Video";
            MetadataRemoteName = "M_VideoV";
            MetadataType = SRAMBMMetadataType.JSON;
            AssetType = SRAssetType.Video;
        }

        internal static new SRMetadataBase CreateInstance(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL, Http.Http httpClient) => new SRVideoMetadata(dictArchiveInfo, baseURL, httpClient);
    }
}
