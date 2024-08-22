namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal class SRDesignMetadata : SRLuaMetadata
    {
        protected SRDesignMetadata(string baseURL) : base(baseURL)
        {
            AssetProperty = new SRAssetProperty();
            ParentRemotePath = "/client/Windows";
            MetadataPath = "/M_DesignV.bytes";
            InheritedAssetType = SRAssetType.DesignData;
        }

        internal static new SRMetadataBase CreateInstance(string baseURL) => new SRDesignMetadata(baseURL);
    }
}
