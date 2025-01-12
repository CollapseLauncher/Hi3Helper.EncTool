// ReSharper disable InconsistentNaming
namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal sealed class SRDesignMetadata : SRLuaMetadata
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
