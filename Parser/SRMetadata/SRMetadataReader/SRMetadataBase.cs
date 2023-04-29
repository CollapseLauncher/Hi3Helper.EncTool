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
        public string MetadataRemoteURL { get; set; }
        public string MetadataLocalName { get; set; }
        public MemoryStream MetadataStream { get; set; }
        public uint MetadataRevision { get; set; }
        public long AssetTotalSize { get => AssetList.Count == 0 ? 0 : AssetList.Sum(x => x.Size); }
        public List<SRAsset> AssetList { get; set; }

        public SRAssetProperty()
        {
            MetadataStream = new MemoryStream();
            AssetList = new List<SRAsset>();
        }
    }

    public class SRAsset
    {
        public string LocalName { get; set; }
        public string RemoteURL { get; set; }
        public long Size { get; set; }
        public byte[] Hash { get; set; }
        public SRAssetType AssetType { get; set; }
    }

    public enum SRAssetType
    {
        IFix,
        Design,
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

        protected Http.Http _httpClient { get; init; }

        protected SRMetadataBase(string baseURL, Http.Http httpClient)
        {
            BaseURL = baseURL;
            _httpClient = httpClient;
        }

        internal virtual async Task GetRemoteMetadata(CancellationToken token)
        {
            AssetProperty = new SRAssetProperty();
            string metadataURL = BaseURL + ParentRemotePath + MetadataPath;

            await _httpClient.Download(metadataURL, AssetProperty.MetadataStream, null, null, token);
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
        internal abstract void Dispose(bool Disposing);
        internal string GetMetadataPathFromArchiveInfo(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string key)
        {
            if (!dictArchiveInfo.ContainsKey(key)) throw new KeyNotFoundException($"Key: {key} in ArchiveInfo dictionary does not exist!");

            SRDispatchArchiveInfo archiveInfo = dictArchiveInfo[key];
            ReadOnlySpan<char> baseName = archiveInfo.FileName.AsSpan().Slice(2);

            return '/' + string.Concat(baseName, new char[] {'_'}, archiveInfo.ContentHash, ".bytes");
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
