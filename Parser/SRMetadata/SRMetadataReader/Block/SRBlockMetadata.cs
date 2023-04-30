﻿using System.Collections.Generic;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal class SRBlockMetadata : SRAsbMetadata
    {
        protected SRBlockMetadata(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL, Http.Http httpClient) : base(dictArchiveInfo, baseURL, httpClient)
        {
            MetadataRemoteName = "M_BlockV";
            MetadataStartRemoteName = "M_Start_BlockV";
            MetadataType = SRAMBMMetadataType.SRBM;
            AssetType = SRAssetType.Block;
        }

        internal static new SRMetadataBase CreateInstance(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL, Http.Http httpClient) => new SRBlockMetadata(dictArchiveInfo, baseURL, httpClient);
    }
}
