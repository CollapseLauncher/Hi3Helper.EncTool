using System.Collections.Generic;
// ReSharper disable InconsistentNaming
// ReSharper disable PartialTypeWithSinglePart

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal sealed partial class SRBlockMetadata : SRAsbMetadata
    {
        private SRBlockMetadata(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL, string baseURLAlt) : base(dictArchiveInfo, baseURL, baseURLAlt)
        {
            MetadataRemoteName = "M_BlockV";
            MetadataStartRemoteName = "M_Start_BlockV";
            MetadataType = SRAMBMMetadataType.SRBM;
            AssetType = SRAssetType.Block;
        }

        internal new static SRMetadataBase CreateInstance(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL, string baseURLAlt) => new SRBlockMetadata(dictArchiveInfo, baseURL, baseURLAlt);
    }
}
