using Hi3Helper.Data;
using Hi3Helper.UABT.Binary;
using System;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal class SRLuaMetadata : SRIFixMetadata
    {
        protected SRAssetType InheritedAssetType { get; set; }

        protected SRLuaMetadata(string baseURL, Http.Http httpClient) : base(baseURL, httpClient, 3)
        {
            AssetProperty = new SRAssetProperty();
            ParentRemotePath = "/client/Windows";
            MetadataPath = "/M_LuaV.bytes";
            InheritedAssetType = SRAssetType.Lua;
        }

        internal static new SRMetadataBase CreateInstance(string baseURL, Http.Http httpClient) => new SRLuaMetadata(baseURL, httpClient);

        internal override void Deserialize()
        {
            using (EndianBinaryReader reader = new EndianBinaryReader(AssetProperty.MetadataStream, UABT.EndianType.BigEndian, false))
            {
                uint toSeekPos = 20;
                uint count = reader.ReadUInt32();
                uint bufferLen = 0;
                uint ver = 0;

                if (count == 255)
                {
                    ver = reader.ReadUInt32();
                    count = reader.ReadUInt32();
                    bufferLen = reader.ReadUInt32();
                    toSeekPos = 12;
#if DEBUG
                    Console.WriteLine($"Switching to read new metadata format!");
#endif
                }

                ReadOnlySpan<byte> empty = new byte[4];
#if DEBUG
                Console.WriteLine($"{InheritedAssetType} Assets Parsed Info: ({reader.BaseStream.Length} bytes) ({count} assets)");
#endif

                for (int i = 0; i < count; i++)
                {
                    long lastPos = reader.Position;
                    byte[] assetID = reader.ReadBytes(4);
                    byte[] hash = reader.ReadBytes(16);
                    uint type = reader.ReadUInt32();
                    uint size = reader.ReadUInt32();
                    uint insideCount = reader.ReadUInt32();

                    // toSeekPos        = Number of bytes to read the asset's content
                    // insideCount      = Number of content count inside the asset
                    // 1                = Unknown offset, seek +1
                    uint toSeek = (insideCount * toSeekPos) + 1;
                    reader.Position += toSeek;

                    string hashName = HexTool.BytesToHexUnsafe(hash);
                    string assetName = $"{hashName}.bytes";
                    AssetProperty.AssetList.Add(new SRAsset
                    {
                        AssetType = InheritedAssetType,
                        Hash = hash,
                        LocalName = assetName,
                        RemoteURL = BaseURL + ParentRemotePath + '/' + assetName,
                        Size = size
                    });
#if DEBUG
                    Console.WriteLine($"    Mark: {HexTool.BytesToHexUnsafe(assetID)} {hashName} -> Size: {size} Pos: {lastPos} Seek: {toSeek} Count: {insideCount}");
#endif
                }

#if DEBUG
                if (reader.Position < reader.BaseStream.Length)
                {
                    throw new IndexOutOfRangeException("SANITY CHECK: reader.Position is not the same as reader.BaseStream.Length. There might be some additional data here");
                }
#endif
            }
        }
    }
}
