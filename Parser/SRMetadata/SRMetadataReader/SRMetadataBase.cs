using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.UABT.Binary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    public class SRAssetProperty
    {
        public string BaseURL { get; set; }
        public string StartBaseURL { get; set; }
        public string MetadataRemoteURL { get; set; }
        public string MetadataStartRemoteURL { get; set; }
        public string MetadataLocalName { get; set; }
        public string MetadataStartLocalName { get; set; }
        public Stream MetadataStream { get; set; }
        public Stream MetadataStartStream { get; set; }
        public uint MetadataRevision { get; set; }
        public uint MetadataStartRevision { get; set; }
        public long AssetTotalSize { get => AssetList.Count == 0 ? 0 : AssetList.Sum(x => x.Size); }
        public List<SRAsset> AssetList { get; set; }

        public SRAssetProperty()
        {
            MetadataStream = new MemoryStream();
            MetadataStartStream = new MemoryStream();
            AssetList = new List<SRAsset>();
        }

        public SRAssetProperty(string metadataPath, string metadataStartPath = null)
        {
            string metadataFolder = Path.GetDirectoryName(metadataPath);
            if (!Directory.Exists(metadataFolder))
            {
                Directory.CreateDirectory(metadataFolder);
            }

            MetadataStream = new FileStream(metadataPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (!string.IsNullOrEmpty(metadataStartPath))
            {
                string metadataStartFolder = Path.GetDirectoryName(metadataStartPath);
                if (!Directory.Exists(metadataStartFolder))
                {
                    Directory.CreateDirectory(metadataStartFolder);
                }
                MetadataStartStream = new FileStream(metadataStartPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            }
            AssetList = new List<SRAsset>();
        }
    }

    public class SRAsset : IAssetIndexSummary
    {
        public string LocalName { get; set; }
        public string RemoteURL { get; set; }
        public long Size { get; set; }
        public byte[] Hash { get; set; }
        public SRAssetType AssetType { get; set; }
        public bool IsPatch { get; set; }

        public string PrintSummary() => $"File [T: {AssetType}]: {LocalName}\t{ConverterTool.SummarizeSizeSimple(Size)} ({Size} bytes)";
        public long GetAssetSize() => Size;
        public string GetRemoteURL() => RemoteURL;
        public void SetRemoteURL(string url) => RemoteURL = url;
    }

    public enum SRAssetType
    {
        IFix,
        DesignData,
        NativeData,
        Asb,
        Block,
        Lua,
        Video,
        Audio
    }

    public abstract class SRMetadataBase : IDisposable
    {
        protected abstract SRAssetProperty AssetProperty { get; set; }
        internal virtual string Magic { get; set; }
        internal virtual ushort TypeID { get; set; }
        protected abstract string ParentRemotePath { get; set; }
        protected abstract string MetadataPath { get; set; }
        protected string BaseURL { get; init; }
        protected string PersistentPath { get; set; }

        protected SRMetadataBase(string baseURL)
        {
            BaseURL = baseURL;
            if (string.IsNullOrEmpty(BaseURL)) throw new NullReferenceException("BaseURL is empty!");
        }

        internal virtual async Task GetRemoteMetadata(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string persistentPath, CancellationToken token, string localManifestPath)
        {
            PersistentPath = persistentPath;
            string metadataPath = Path.Combine(PersistentPath, localManifestPath, MetadataPath.TrimStart('/'));
            string metadataURL = BaseURL + ParentRemotePath + MetadataPath;

            AssetProperty = new SRAssetProperty(metadataPath);

            #if DEBUG
            Console.WriteLine($"[SRMetadataBase:GetRemoteData] Fetching metadata from {metadataURL}");
            #endif
            await downloadClient.DownloadAsync(metadataURL, AssetProperty.MetadataStream, false, downloadProgressDelegate, cancelToken: token);
            AssetProperty.MetadataStream.Seek(0, SeekOrigin.Begin);
        }

        protected void EnsureMagicIsValid(EndianBinaryReader reader)
        {
            string magic = reader.ReadAlignedString(4);
            if (!magic.Equals(Magic)) throw new FormatException($"Magic is not valid for metadata: {Magic}. Getting {magic} instead");

            ushort typeID = reader.ReadUInt16();
            if (typeID != TypeID) throw new FormatException($"TypeID for metadata: {Magic} is not valid. Getting {typeID} while expecting {TypeID}");
        }

        internal abstract void Deserialize();
        public SRAssetProperty GetAssets() => AssetProperty;
        public IEnumerable<SRAsset> EnumerateAssets() => AssetProperty.AssetList;
        internal abstract void Dispose(bool Disposing);
        internal string GetMetadataPathFromArchiveInfo(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string key)
        {
            if (!dictArchiveInfo.ContainsKey(key)) throw new KeyNotFoundException($"Key: {key} in ArchiveInfo dictionary does not exist!");

            SRDispatchArchiveInfo archiveInfo = dictArchiveInfo[key];
            ReadOnlySpan<char> baseName = archiveInfo.FileName.AsSpan().Slice(2);

            return '/' + string.Concat(baseName, new char[] { '_' }, archiveInfo.ContentHash, ".bytes");
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
