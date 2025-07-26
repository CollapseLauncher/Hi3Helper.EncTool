using Hi3Helper.Data;
using Hi3Helper.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal partial class SRAsbMetadata : SRMetadataBase
    {
        private readonly Dictionary<string, SRDispatchArchiveInfo> _dispatchArchiveInfo;
        private          SRAMBMMetadataStruct                      _structSRAMData;

        protected          string             MetadataStartRemoteName = "M_Start_AsbV";
        protected          string             MetadataRemoteName      = "M_AsbV";
        protected          SRAssetType        AssetType        { get; set; }
        protected          SRAMBMMetadataType MetadataType     { get; set; }
        protected override string             ParentRemotePath { get; set; } = "/client/Windows/Block";
        protected override string             MetadataPath     { get; set; }
        protected override SRAssetProperty    AssetProperty    { get; set; }
        protected SRAsbMetadata(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) : base(baseURL)
        {
            _dispatchArchiveInfo = dictArchiveInfo;
            MetadataType         = SRAMBMMetadataType.SRAM;
            AssetType            = SRAssetType.Asb;
        }

        internal static SRMetadataBase CreateInstance(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) => new SRAsbMetadata(dictArchiveInfo, baseURL);

        internal override async Task GetRemoteMetadata(DownloadClient downloadClient, DownloadProgressDelegate downloadClientProgress, string persistentPath, CancellationToken token, string localManifestPath)
        {
            PersistentPath = persistentPath;
            MetadataPath   = GetMetadataPathFromArchiveInfo(_dispatchArchiveInfo, MetadataRemoteName);

            AssetProperty = MetadataType != SRAMBMMetadataType.JSON ? new SRAssetProperty() : new SRAssetProperty(Path.Combine(persistentPath, localManifestPath, MetadataPath.TrimStart('/')));

            AssetProperty.MetadataRemoteURL = BaseURL + ParentRemotePath + MetadataPath;
            AssetProperty.MetadataRevision  = _dispatchArchiveInfo[MetadataRemoteName].PatchVersion;
            AssetProperty.MetadataLocalName = MetadataPath.TrimStart('/');
            AssetProperty.BaseURL           = _dispatchArchiveInfo[MetadataRemoteName].FullAssetsDownloadUrl;

            if (MetadataType != SRAMBMMetadataType.JSON)
            {
                using (SRAMBMMetadataReader _SRAMReader = new(BaseURL, ParentRemotePath, MetadataPath, MetadataType))
                {
                    await _SRAMReader.GetRemoteMetadata(downloadClient, downloadClientProgress, persistentPath, token, "Asb\\Windows");
                    _SRAMReader.Deserialize();
                    Magic  = _SRAMReader.Magic;
                    TypeID = _SRAMReader.TypeID;

                    _structSRAMData = _SRAMReader.StructList[0];
                }

                string MetadataStartPath = GetMetadataPathFromArchiveInfo(_dispatchArchiveInfo, MetadataStartRemoteName);
                AssetProperty.MetadataStartRemoteURL = BaseURL + ParentRemotePath + MetadataStartPath;
                AssetProperty.MetadataStartRevision  = _dispatchArchiveInfo[MetadataRemoteName].PatchVersion;
                AssetProperty.MetadataStartLocalName = MetadataStartPath.TrimStart('/');
                AssetProperty.StartBaseURL           = BaseURL + ParentRemotePath;

                using (SRAMBMMetadataReader _SRAMReader = new(BaseURL, ParentRemotePath, MetadataStartPath, MetadataType))
                {
                    await _SRAMReader.GetRemoteMetadata(downloadClient, downloadClientProgress, persistentPath, token, "Asb\\Windows");
                }

                return;
            }

            await downloadClient.DownloadAsync(AssetProperty.MetadataRemoteURL, AssetProperty.MetadataStream, false, downloadClientProgress, cancelToken: token);
            AssetProperty.MetadataStream.Seek(0, SeekOrigin.Begin);
        }

        internal override void Deserialize()
        {
            if (_structSRAMData.StructData == null) throw new InvalidOperationException("Struct data is empty! Please initialize it using GetRemoteMetadata()");
            try
            {
                DeserializeAsset();
            }
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
            Console.WriteLine($"{AssetType} Assets Parsed Info: ({refStruct.StructSize} bytes) ({refStruct.StructCount} assets)");
#endif
            // Force the stack allocation

            for (int index = 0; index < refStruct.StructData.Length; index++)
            {
                ReadOnlySpan<byte> bufferSpan = refStruct.StructData[index];
                ReadOnlySpan<byte> hashBuffer = bufferSpan[..16];
                ReadOnlySpan<byte> assetID    = bufferSpan.Slice(16, 4);
                uint               size       = BitConverter.ToUInt32(bufferSpan.Slice(20, 4));

                Span<byte> hash      = BigEndianBytesToHexBytes(hashBuffer);
                string     hashName  = HexTool.BytesToHexUnsafe(hash);
                string     assetName = $"{hashName}.block";

                bool isStart = assetID[2] >> 4 > 0;

#if DEBUG
                Console.WriteLine($"    Mark: {HexTool.BytesToHexUnsafe(assetID)} {hashName} -> Size: {size} Pos: {index} IsStart: {isStart}");
#endif

                SRAsset asset = new()
                {
                    AssetType = AssetType,
                    Hash      = hash.ToArray(),
                    LocalName = assetName,
                    RemoteURL = (isStart ? AssetProperty.StartBaseURL : AssetProperty.BaseURL) + '/' + assetName,
                    Size      = size
                };

                AssetProperty.AssetList.Add(asset);
            }
        }

        private static byte[] BigEndianBytesToHexBytes(ReadOnlySpan<byte> span)
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
