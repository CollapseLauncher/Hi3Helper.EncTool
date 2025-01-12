using System.Collections.Generic;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal sealed class SRBlockMetadata : SRAsbMetadata
    {
        private SRBlockMetadata(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) : base(dictArchiveInfo, baseURL)
        {
            MetadataRemoteName = "M_BlockV";
            MetadataStartRemoteName = "M_Start_BlockV";
            MetadataType = SRAMBMMetadataType.SRBM;
            AssetType = SRAssetType.Block;
        }

        internal new static SRMetadataBase CreateInstance(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) => new SRBlockMetadata(dictArchiveInfo, baseURL);
    }
}
