using System.Collections.Generic;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable PartialTypeWithSinglePart

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal sealed partial class SRVideoMetadata : SRAudioMetadata
    {
        private SRVideoMetadata(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) : base(dictArchiveInfo, baseURL)
        {
            ParentRemotePath   = "/client/Windows/Video";
            MetadataRemoteName = "M_VideoV";
            MetadataType       = SRAMBMMetadataType.JSON;
            AssetType          = SRAssetType.Video;
        }

        internal new static SRMetadataBase CreateInstance(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) => new SRVideoMetadata(dictArchiveInfo, baseURL);
    }
}
