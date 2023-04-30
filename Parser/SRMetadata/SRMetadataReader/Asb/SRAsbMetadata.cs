using Hi3Helper.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal class SRAsbMetadata : SRMetadataBase
    {
        private Dictionary<string, SRDispatchArchiveInfo> _dispatchArchiveInfo;
        private SRAMBMMetadataStruct _structSRAMData;

        protected string MetadataStartRemoteName = "M_Start_AsbV";
        protected string MetadataRemoteName = "M_AsbV";
        protected SRAssetType AssetType { get; set; }
        protected SRAMBMMetadataType MetadataType { get; set; }
        protected override string ParentRemotePath { get; set; }
        protected override string MetadataPath { get; set; }
        protected override SRAssetProperty AssetProperty { get; set; }
        protected SRAsbMetadata(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL, Http.Http httpClient) : base(baseURL, httpClient)
        {
            _dispatchArchiveInfo = dictArchiveInfo;
            ParentRemotePath = "/client/Windows/Block";
            MetadataRemoteName = "M_AsbV";
            MetadataStartRemoteName = "M_Start_AsbV";
            MetadataType = SRAMBMMetadataType.SRAM;
            AssetType = SRAssetType.Asb;
        }

        internal static SRMetadataBase CreateInstance(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL, Http.Http httpClient) => new SRAsbMetadata(dictArchiveInfo, baseURL, httpClient);

        internal override async Task GetRemoteMetadata(string persistentPath, CancellationToken token, string localManifestPath)
        {
            PersistentPath = persistentPath;
            MetadataPath = GetMetadataPathFromArchiveInfo(_dispatchArchiveInfo, MetadataRemoteName);

            if (MetadataType != SRAMBMMetadataType.JSON)
            {
                AssetProperty = new SRAssetProperty();
            }
            else
            {
                AssetProperty = new SRAssetProperty(Path.Combine(persistentPath, localManifestPath, MetadataPath.TrimStart('/')));
            }

            AssetProperty.MetadataRemoteURL = BaseURL + ParentRemotePath + MetadataPath;
            AssetProperty.MetadataRevision = _dispatchArchiveInfo[MetadataRemoteName].PatchVersion;
            AssetProperty.MetadataLocalName = MetadataPath.TrimStart('/');
            AssetProperty.BaseURL = _dispatchArchiveInfo[MetadataRemoteName].FullAssetsDownloadUrl;

            if (MetadataType != SRAMBMMetadataType.JSON)
            {
                using (SRAMBMMetadataReader _SRAMReader = new SRAMBMMetadataReader(BaseURL, _httpClient, ParentRemotePath, MetadataPath, MetadataType))
                {
                    await _SRAMReader.GetRemoteMetadata(persistentPath, token, "Asb\\Windows");
                    _SRAMReader.Deserialize();
                    Magic = _SRAMReader.Magic;
                    TypeID = _SRAMReader.TypeID;

                    _structSRAMData = _SRAMReader.StructList[0];
                }

                string MetadataStartPath = GetMetadataPathFromArchiveInfo(_dispatchArchiveInfo, MetadataStartRemoteName);
                AssetProperty.MetadataStartRemoteURL = BaseURL + ParentRemotePath + MetadataStartPath;
                AssetProperty.MetadataStartRevision = _dispatchArchiveInfo[MetadataRemoteName].PatchVersion;
                AssetProperty.MetadataStartLocalName = MetadataStartPath.TrimStart('/');
                AssetProperty.StartBaseURL = BaseURL + ParentRemotePath;

                using (SRAMBMMetadataReader _SRAMReader = new SRAMBMMetadataReader(BaseURL, _httpClient, ParentRemotePath, MetadataStartPath, MetadataType))
                {
                    await _SRAMReader.GetRemoteMetadata(persistentPath, token, "Asb\\Windows");
                }

                return;
            }

            await _httpClient.Download(AssetProperty.MetadataRemoteURL, AssetProperty.MetadataStream, null, null, token);
            AssetProperty.MetadataStream.Seek(0, SeekOrigin.Begin);
        }

        internal override void Deserialize()
        {
            if (_structSRAMData.structData == null) throw new InvalidOperationException($"Struct data is empty! Please initialize it using GetRemoteMetadata()");
            try
            {
                DeserializeAsset();
            }
            catch { throw; }
            finally
            {
                _structSRAMData.ClearStruct();
            }
        }

        private void DeserializeAsset()
        {
            if (AssetType == SRAssetType.Asb) return;

            SRAMBMMetadataStruct refStruct = _structSRAMData;
#if DEBUG
            Console.WriteLine($"{AssetType} Assets Parsed Info: ({refStruct.structSize} bytes) ({refStruct.structCount} assets)");
#endif
            Span<byte> hash = stackalloc byte[16];

            for (int index = 0; index < refStruct.structData.Length; index++)
            {
                ReadOnlySpan<byte> bufferSpan = refStruct.structData[index];
                ReadOnlySpan<byte> hashBuffer = bufferSpan.Slice(0, 16);
                ReadOnlySpan<byte> assetID = bufferSpan.Slice(16, 4);
                uint size = HexTool.BytesToUInt32Unsafe(bufferSpan.Slice(20, 4));

                hash = BigEndianBytesToHexBytes(hashBuffer);
                string hashName = HexTool.BytesToHexUnsafe(hash);
                string assetName = $"{hashName}.block";

                bool isStart = (assetID[2] >> 4) > 0;

#if DEBUG
                Console.WriteLine($"    Mark: {HexTool.BytesToHexUnsafe(assetID)} {hashName} -> Size: {size} Pos: {index} IsStart: {isStart}");
#endif

                SRAsset asset = new SRAsset
                {
                    AssetType = AssetType,
                    Hash = hash.ToArray(),
                    LocalName = assetName,
                    RemoteURL = (isStart ? AssetProperty.StartBaseURL : AssetProperty.BaseURL) + '/' + assetName,
                    Size = size
                };

                AssetProperty.AssetList.Add(asset);
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
            AssetProperty?.MetadataStartStream?.Dispose();
            AssetProperty?.AssetList?.Clear();
            AssetProperty = null;
        }
    }
}
