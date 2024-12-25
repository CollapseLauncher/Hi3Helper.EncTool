using System.Collections.Generic;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal partial class SRBlockMetadata : SRAsbMetadata
    {
        protected SRBlockMetadata(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) : base(dictArchiveInfo, baseURL)
        {
            MetadataRemoteName = "M_BlockV";
            MetadataStartRemoteName = "M_Start_BlockV";
            MetadataType = SRAMBMMetadataType.SRBM;
            AssetType = SRAssetType.Block;
        }

        internal static new SRMetadataBase CreateInstance(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) => new SRBlockMetadata(dictArchiveInfo, baseURL);
    }
}
