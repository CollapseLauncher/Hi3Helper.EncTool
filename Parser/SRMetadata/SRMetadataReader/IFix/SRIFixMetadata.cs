using Hi3Helper.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal class SRIFixMetadata : SRMetadataBase
    {
        protected override string ParentRemotePath { get; set; }
        protected override string MetadataPath { get; set; }
        protected override SRAssetProperty AssetProperty { get; set; }
        protected SRIFixMetadata(string baseURL, Http.Http httpClient) : base(baseURL, httpClient)
        {
            AssetProperty = new SRAssetProperty();
            ParentRemotePath = "/client/Windows";
            MetadataPath = "/M_IFixV.bytes";
        }

        internal static SRMetadataBase CreateInstance(string baseURL, Http.Http httpClient) => new SRIFixMetadata(baseURL, httpClient);

        internal override async Task GetRemoteMetadata(CancellationToken token)
        {
            using (SRMIMetadataReader _SRMIReader = new SRMIMetadataReader(BaseURL, _httpClient, ParentRemotePath, MetadataPath))
            {
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
            }
        }

        internal override void Deserialize()
        {
            using (StreamReader reader = new StreamReader(AssetProperty.MetadataStream, Encoding.UTF8, true, -1, true))
            {
                while (!reader.EndOfStream)
                {
                    string[] tuple = reader.ReadLine().Split(',');
                    AssetProperty.AssetList.Add(new SRAsset
                    {
                        AssetType = SRAssetType.IFix,
                        Hash = HexTool.HexToBytesUnsafe(tuple[1]),
                        Size = long.Parse(tuple[2]),
                        LocalName = tuple[0],
                        RemoteURL = BaseURL + ParentRemotePath + '/' + tuple[0]
                    });
                }
            }
        }

        internal override void Dispose(bool Disposing)
        {
            AssetProperty?.MetadataStream?.Dispose();
            AssetProperty?.AssetList?.Clear();
            AssetProperty = null;
        }
    }
}
