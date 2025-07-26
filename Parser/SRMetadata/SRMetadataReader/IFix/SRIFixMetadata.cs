using Hi3Helper.Data;
using Hi3Helper.Http;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Resharper disable all

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal partial class SRIFixMetadata : SRMetadataBase
    {
        private ushort SRMIID { get; set; }

        protected override string ParentRemotePath { get; set; }
        protected override string MetadataPath { get; set; }
        protected override SRAssetProperty AssetProperty { get; set; }
        protected SRIFixMetadata(string baseURL, ushort idSRMI = 2) : base(baseURL)
        {
            ParentRemotePath = "/client/Windows";
            MetadataPath = "/M_IFixV.bytes";
            SRMIID = idSRMI;
        }

        internal static SRMetadataBase CreateInstance(string baseURL) => new SRIFixMetadata(baseURL);

        internal override async Task GetRemoteMetadata(DownloadClient downloadClient, DownloadProgressDelegate downloadClientProgress, string persistentPath, CancellationToken token, string localManifestPath)
        {
            PersistentPath = persistentPath;
            using (SRMIMetadataReader _SRMIReader = new SRMIMetadataReader(BaseURL, ParentRemotePath, MetadataPath, SRMIID))
            {
                await _SRMIReader.GetRemoteMetadata(downloadClient, downloadClientProgress, persistentPath, token, localManifestPath);
                _SRMIReader.Deserialize();
                string metadataPath = Path.Combine(persistentPath, localManifestPath, _SRMIReader.AssetListFilename);
                string metadataURL = BaseURL + ParentRemotePath + '/' + _SRMIReader.AssetListFilename;

                AssetProperty = new SRAssetProperty(metadataPath);
                AssetProperty.BaseURL = BaseURL + ParentRemotePath;
                AssetProperty.MetadataRemoteURL = metadataURL;
                AssetProperty.MetadataRevision = _SRMIReader.RemoteRevisionID;
                AssetProperty.MetadataLocalName = _SRMIReader.AssetListFilename;
                Magic = _SRMIReader.Magic;
                TypeID = _SRMIReader.TypeID;

                await downloadClient.PerformCopyToDownload(metadataURL, downloadClientProgress, AssetProperty.MetadataStream, token);
                AssetProperty.MetadataStream.Position = 0;
            }
        }

        internal override void Deserialize()
        {
            using (StreamReader reader = new StreamReader(AssetProperty.MetadataStream, Encoding.UTF8, true, -1, false))
            {
                while (!reader.EndOfStream)
                {
                    string[] tuple = reader.ReadLine().Split(',');
                    if (tuple[0] == "empty")
                    {
                        Console.Write("SRIFix Cache returning empty! Skipping...\r\n");
                        return;
                    }
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
