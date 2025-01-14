// ReSharper disable InconsistentNaming
// ReSharper disable PartialTypeWithSinglePart
namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal sealed partial class SRDesignMetadata : SRLuaMetadata
    {
        private SRDesignMetadata(string baseURL) : base(baseURL)
        {
            AssetProperty = new SRAssetProperty();
            ParentRemotePath = "/client/Windows";
            MetadataPath = "/M_DesignV.bytes";
            InheritedAssetType = SRAssetType.DesignData;
        }

        internal new static SRMetadataBase CreateInstance(string baseURL) => new SRDesignMetadata(baseURL);
    }
}
