using Hi3Helper.Data;
using Hi3Helper.UABT.Binary;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal class SRLuaMetadata : SRMetadataBase
    {
        private SRMetadataInfo _SRMIReader { get; set; }

        protected override string ParentRemotePath { get; set; }
        protected override string MetadataPath { get; set; }
        protected override SRAssetProperty AssetProperty { get; set; }
        protected SRLuaMetadata(string baseURL, Http.Http httpClient) : base(baseURL, httpClient)
        {
            AssetProperty = new SRAssetProperty();
            ParentRemotePath = "/client/Windows";
            MetadataPath = "/M_LuaV.bytes";
        }

        internal static SRMetadataBase CreateInstance(string baseURL, Http.Http httpClient) => new SRLuaMetadata(baseURL, httpClient);

        internal override async Task GetRemoteMetadata(CancellationToken token)
        {
            _SRMIReader = new SRMetadataInfo(BaseURL, _httpClient, ParentRemotePath, MetadataPath);

            await _SRMIReader.GetRemoteMetadata(token);
            _SRMIReader.Deserialize();
            string metadataURL = BaseURL + ParentRemotePath + '/' + _SRMIReader.AssetListFilename;
            AssetProperty.BaseURL = BaseURL + ParentRemotePath;
            AssetProperty.MetadataRemoteURL = metadataURL;
            AssetProperty.MetadataRevision = _SRMIReader.RemoteRevisionID;
            AssetProperty.MetadataLocalName = _SRMIReader.AssetListFilename;
            Magic = _SRMIReader.Magic;
            TypeID = _SRMIReader.TypeID;

            await _httpClient.Download(AssetProperty.MetadataRemoteURL, AssetProperty.MetadataStream, null, null, token);
            AssetProperty.MetadataStream.Seek(0, SeekOrigin.Begin);

            _SRMIReader?.Dispose();
        }

        internal override void Deserialize()
        {
            using (EndianBinaryReader reader = new EndianBinaryReader(AssetProperty.MetadataStream, UABT.EndianType.BigEndian, true))
            {
                uint count = reader.ReadUInt32();
                ReadOnlySpan<byte> empty = new byte[4];
#if DEBUG
                Console.WriteLine($"Lua Assets Parsed Info: ({reader.BaseStream.Length} bytes) ({count} assets)");
#endif

                for (int i = 0; i < count; i++)
                {
                    long lastPos = reader.Position;
                    ReadOnlySpan<byte> assetID = reader.ReadBytes(4);
                    ReadOnlySpan<byte> hash = reader.ReadBytes(16);
                    uint type = reader.ReadUInt32();
                    uint size = reader.ReadUInt32();
                    uint insideCount = reader.ReadUInt32();

                    // 20               = Number of bytes to read the asset's content
                    // insideCount      = Number of content count inside the asset
                    // 1                = Unknown offset, seek +1
                    uint toSeek = (insideCount * 20) + 1;
                    reader.Position += toSeek;

                    string hashName = HexTool.BytesToHexUnsafe(hash);
                    string assetName = $"{hashName}.bytes";
                    AssetProperty.AssetList.Add(new SRAsset
                    {
                        AssetType = SRAssetType.Lua,
                        Hash = hash.ToArray(),
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

        internal override void Dispose(bool Disposing)
        {
            _SRMIReader?.Dispose();
            AssetProperty?.MetadataStream?.Dispose();
            AssetProperty?.AssetList?.Clear();
            AssetProperty = null;
        }
    }
}
