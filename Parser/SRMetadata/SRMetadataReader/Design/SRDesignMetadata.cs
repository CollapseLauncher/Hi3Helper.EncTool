namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal class SRDesignMetadata : SRLuaMetadata
    {
        protected SRDesignMetadata(string baseURL, Http.Http httpClient) : base(baseURL, httpClient)
        {
            AssetProperty = new SRAssetProperty();
            ParentRemotePath = "/client/Windows";
            MetadataPath = "/M_DesignV.bytes";
            InheritedAssetType = SRAssetType.DesignData;
        }

        internal static new SRMetadataBase CreateInstance(string baseURL, Http.Http httpClient) => new SRDesignMetadata(baseURL, httpClient);
    }
}
