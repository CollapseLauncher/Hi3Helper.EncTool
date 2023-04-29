using Hi3Helper.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal class SRAsbMetadata : SRMetadataBase
    {
        private Dictionary<string, SRDispatchArchiveInfo> _dispatchArchiveInfo;
        private SRAMBMMetadataStruct _structSRAMData;

        protected string MetadataRemoteName = "M_AsbV";
        protected SRAssetType AssetType { get; set; }
        protected SRAMBMMetadataType MetadataType { get; set; }
        protected override string ParentRemotePath { get; set; }
        protected override string MetadataPath { get; set; }
        protected override SRAssetProperty AssetProperty { get; set; }
        protected SRAsbMetadata(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL, Http.Http httpClient) : base(baseURL, httpClient)
        {
            AssetProperty = new SRAssetProperty();
            _dispatchArchiveInfo = dictArchiveInfo;
            ParentRemotePath = "/client/Windows/Block";
            MetadataRemoteName = "M_AsbV";
            MetadataType = SRAMBMMetadataType.SRAM;
            AssetType = SRAssetType.Asb;
        }

        internal static SRMetadataBase CreateInstance(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL, Http.Http httpClient) => new SRAsbMetadata(dictArchiveInfo, baseURL, httpClient);

        internal override async Task GetRemoteMetadata(CancellationToken token)
        {
            MetadataPath = GetMetadataPathFromArchiveInfo(_dispatchArchiveInfo, MetadataRemoteName);

            AssetProperty.MetadataRemoteURL = BaseURL + ParentRemotePath + MetadataPath;
            AssetProperty.MetadataRevision = _dispatchArchiveInfo[MetadataRemoteName].PatchVersion;
            AssetProperty.MetadataLocalName = MetadataPath.TrimStart('/');
            AssetProperty.BaseURL = _dispatchArchiveInfo[MetadataRemoteName].FullAssetsDownloadUrl;

            if (MetadataType != SRAMBMMetadataType.JSON)
            {
                using (SRAMBMMetadataReader _SRAMReader = new SRAMBMMetadataReader(BaseURL, _httpClient, ParentRemotePath, MetadataPath, MetadataType))
                {
                    await _SRAMReader.GetRemoteMetadata(token);
                    _SRAMReader.Deserialize();
                    Magic = _SRAMReader.Magic;
                    TypeID = _SRAMReader.TypeID;

                    _structSRAMData = _SRAMReader.StructList[0];
                }

                return;
            }

            await _httpClient.Download(AssetProperty.MetadataRemoteURL, AssetProperty.MetadataStream, null, null, token);
            AssetProperty.MetadataStream.Seek(0, System.IO.SeekOrigin.Begin);
        }

        internal override void Deserialize()
        {
            if (_structSRAMData.structData == null) throw new InvalidOperationException($"Struct data is empty! Please initialize it using GetRemoteMetadata()");
            try
            {
#if DEBUG
                Console.WriteLine($"{AssetType} Assets Parsed Info: ({_structSRAMData.structSize} bytes) ({_structSRAMData.structCount} assets)");
#endif
                Span<byte> hash = stackalloc byte[16];

                for (int index = 0; index < _structSRAMData.structData.Length; index++)
                {
                    ReadOnlySpan<byte> bufferSpan = _structSRAMData.structData[index];
                    ReadOnlySpan<byte> hashBuffer = bufferSpan.Slice(0, 16);
                    ReadOnlySpan<byte> assetID = bufferSpan.Slice(16, 4);
                    uint size = HexTool.BytesToUInt32Unsafe(bufferSpan.Slice(20, 4));

                    hash = BigEndianBytesToHexBytes(hashBuffer);
                    string hashName = HexTool.BytesToHexUnsafe(hash);
                    string assetName = $"{hashName}.block";

#if DEBUG
                    Console.WriteLine($"    Mark: {HexTool.BytesToHexUnsafe(assetID)} {hashName} -> Size: {size} Pos: {index}");
#endif

                    AssetProperty.AssetList.Add(new SRAsset
                    {
                        AssetType = AssetType,
                        Hash = hash.ToArray(),
                        LocalName = assetName,
                        RemoteURL = AssetProperty.BaseURL + '/' + assetName,
                        Size = size
                    });
                }
            }
            catch { throw; }
            finally
            {
                _structSRAMData.ClearStruct();
            }
        }

        private byte[] BigEndianBytesToHexBytes(ReadOnlySpan<byte> span)
        {
            byte[] result = new byte[16];

            for (
                int i = 0, k = 0;
                i < 4;
                i++)
            {
                ReadOnlySpan<byte> hashC = span.Slice(k, 4);
                for (
                    int j = 3;
                    j >= 0;
                    j--, k++)
                {
                    result[k] = hashC[j];
                }
            }

            return result;
        }

        internal override void Dispose(bool Disposing)
        {
            AssetProperty?.MetadataStream?.Dispose();
            AssetProperty?.AssetList?.Clear();
            AssetProperty = null;
        }
    }
}
